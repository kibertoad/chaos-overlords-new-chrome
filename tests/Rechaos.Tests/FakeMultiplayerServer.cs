using System.Net;
using System.Text;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Tests;

/// <summary>
/// A coordination server that answers whatever a test wants it to, over a real
/// <see cref="HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// Stubbing the transport rather than the client is what makes these tests worth having. The session
/// is the part of the protocol nothing else covers — what it retries, what it drops as a repeat, what
/// it treats as the end of a match — and all of that is decided by how it reacts to real responses
/// arriving in a real order. A fake session interface would have tested the test.
/// </para>
/// <para>
/// Requests are recorded so a test can assert what the client said, not just what it did with the
/// answer: reporting a state hash is the whole point of applying a turn, and nothing local shows
/// whether it happened.
/// </para>
/// </remarks>
internal sealed class FakeMultiplayerServer : HttpMessageHandler
{
    private readonly List<Recorded> _requests = [];
    private readonly Dictionary<string, Queue<Reply>> _queued = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Reply> _standing = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<TaskCompletionSource>> _blocks = new(StringComparer.Ordinal);
    private readonly List<PushStream> _connections = [];
    private readonly Lock _gate = new();
    private PushStream _events = new();

    /// <summary>
    /// The event stream's body, which a test writes frames into.
    /// </summary>
    /// <remarks>
    /// One body per connection. A body the client has let go of — because it dropped the connection
    /// itself, or because a test ended it — is not handed out again: the next request for the
    /// stream gets a fresh one, as a server would give it, so a test can drive a reconnect and then
    /// keep writing.
    /// </remarks>
    internal PushStream Events
    {
        get { lock (_gate) return _events; }
    }

    /// <summary>
    /// When set, every stream request is accepted and closed at once, before a single frame.
    /// </summary>
    internal bool CloseStreamOnOpen { get; set; }

    /// <summary>Ends the connection being read, as a server dropping it would.</summary>
    internal void DropStream()
    {
        PushStream current;
        lock (_gate) current = _events;
        current.End();
    }

    /// <summary>Every request the client has made, in order.</summary>
    internal IReadOnlyList<Recorded> Requests
    {
        get { lock (_gate) return [.. _requests]; }
    }

    public FakeMultiplayerServer()
    {
        Answer(
            HttpMethod.Post,
            "/handshake",
            new HandshakeResponse(MultiplayerProtocolVersion.Current));
    }

    /// <summary>What to answer every time this route is called.</summary>
    internal void Answer(HttpMethod method, string pathSuffix, object? body, HttpStatusCode status = HttpStatusCode.OK)
    {
        lock (_gate) _standing[Key(method, pathSuffix)] = new Reply(status, Serialize(body));
    }

    /// <summary>
    /// What to answer the next time this route is called, before the standing answer.
    /// </summary>
    /// <remarks>
    /// Queued replies are consumed in order, which is how a test says "fail once, then succeed" — the
    /// shape every retry in the session has to survive.
    /// </remarks>
    internal void AnswerOnce(
        HttpMethod method,
        string pathSuffix,
        object? body,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        lock (_gate)
        {
            if (!_queued.TryGetValue(Key(method, pathSuffix), out var queue))
            {
                queue = new Queue<Reply>();
                _queued[Key(method, pathSuffix)] = queue;
            }
            queue.Enqueue(new Reply(status, Serialize(body)));
        }
    }

    /// <summary>Blocks the next matching request until released, while still honoring cancellation.</summary>
    internal TaskCompletionSource BlockOnce(HttpMethod method, string pathSuffix)
    {
        var block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            var key = Key(method, pathSuffix);
            if (!_blocks.TryGetValue(key, out var queue))
            {
                queue = new Queue<TaskCompletionSource>();
                _blocks[key] = queue;
            }
            queue.Enqueue(block);
        }
        return block;
    }

    /// <summary>How many times a route has been called.</summary>
    internal int CallsTo(HttpMethod method, string pathSuffix)
    {
        lock (_gate)
        {
            return _requests.Count(
                request => request.Method == method
                    && request.Path.EndsWith(pathSuffix, StringComparison.Ordinal));
        }
    }

    /// <summary>The bodies a route was called with, in order.</summary>
    internal IReadOnlyList<string> BodiesSentTo(HttpMethod method, string pathSuffix)
    {
        lock (_gate)
        {
            return _requests
                .Where(request => request.Method == method
                    && request.Path.EndsWith(pathSuffix, StringComparison.Ordinal))
                .Select(request => request.Body)
                .ToArray();
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var lastEventId = request.Headers.TryGetValues("Last-Event-ID", out var values)
            ? values.SingleOrDefault()
            : null;
        lock (_gate)
        {
            _requests.Add(new Recorded(
                request.Method, path, body, lastEventId, request.RequestUri.Query));
        }

        var block = NextBlock(request.Method, path);
        if (block is not null)
            await block.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        // A queued or standing answer wins even for the stream route, so a test can play the
        // things that sit between a player and the server: a reverse proxy answering 404 for every
        // path while the backend restarts, a tunnel that has gone stale. Those produce a status
        // with no error envelope, which is a different fact from the server refusing.
        var reply = Next(request.Method, path);
        if (reply is not null) return Json(reply.Status, reply.Body);
        if (path.EndsWith("/stream", StringComparison.Ordinal)) return Streaming();
        return Json(HttpStatusCode.NotFound, UnroutedEnvelope);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                _events.Dispose();
                foreach (var connection in _connections) connection.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    private Reply? Next(HttpMethod method, string path)
    {
        lock (_gate)
        {
            foreach (var (key, queue) in _queued)
            {
                if (!Matches(key, method, path) || queue.Count == 0) continue;
                return queue.Dequeue();
            }
            foreach (var (key, reply) in _standing)
            {
                if (Matches(key, method, path)) return reply;
            }
            return null;
        }
    }

    private TaskCompletionSource? NextBlock(HttpMethod method, string path)
    {
        lock (_gate)
        {
            foreach (var (key, queue) in _blocks)
            {
                if (Matches(key, method, path) && queue.Count > 0) return queue.Dequeue();
            }
            return null;
        }
    }

    private static bool Matches(string key, HttpMethod method, string path)
    {
        var separator = key.IndexOf(' ', StringComparison.Ordinal);
        return key.AsSpan(0, separator).SequenceEqual(method.Method)
            && path.EndsWith(key[(separator + 1)..], StringComparison.Ordinal);
    }

    private HttpResponseMessage Streaming()
    {
        PushStream body;
        lock (_gate)
        {
            if (_events.Ended)
            {
                _connections.Add(_events);
                _events = new PushStream();
            }
            body = _events;
            if (CloseStreamOnOpen) body.End();
        }
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(body),
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "text/event-stream");
        return response;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// What a route nothing answered replies with: the server's own error envelope.
    /// </summary>
    /// <remarks>
    /// An envelope rather than a bare status, because the real server's error handler always writes
    /// one and the client now reads the difference. A status with no envelope is what something
    /// BETWEEN the player and the server answers — a proxy, a tunnel, another service on the port —
    /// and the client treats that as an outage rather than a verdict. A fake that answered bare
    /// statuses for its own unrouted paths would have been playing the proxy by accident.
    /// </remarks>
    private const string UnroutedEnvelope =
        """
        {"error":{"code":"not_found","message":"no such route","details":{"reason":"not_found"}}}
        """;

    private static string Serialize(object? body) =>
        body is null ? "{}" : body as string ?? WireJson.Write(body);

    private static string Key(HttpMethod method, string pathSuffix) => $"{method.Method} {pathSuffix}";

    private sealed record Reply(HttpStatusCode Status, string Body);

    /// <summary>One request the client made.</summary>
    /// <param name="Query">The query string, with its leading <c>?</c>, or empty.</param>
    internal sealed record Recorded(
        HttpMethod Method,
        string Path,
        string Body,
        string? LastEventId,
        string Query);
}

/// <summary>
/// A response body a test writes into while the client is reading it.
/// </summary>
/// <remarks>
/// The event stream is the one part of the protocol whose timing is the thing being tested: a frame
/// has to arrive after the session has started reading, and the session has to still be reading
/// afterwards. A <see cref="MemoryStream"/> would end the moment the prepared frames ran out, which
/// the client correctly treats as a dropped connection and reconnects — so the test would be racing
/// the reconnect instead of exercising what it meant to.
/// </remarks>
internal sealed class PushStream : Stream
{
    private readonly SemaphoreSlim _available = new(0);
    private readonly Queue<byte[]> _chunks = new();
    private readonly Lock _gate = new();
    private byte[] _current = [];
    private int _offset;
    private bool _ended;

    /// <summary>Makes a chunk of body available to whoever is reading.</summary>
    internal void Write(string text)
    {
        lock (_gate) _chunks.Enqueue(Encoding.UTF8.GetBytes(text));
        _available.Release();
    }

    /// <summary>Ends the body, as a server closing the connection would. Safe to call twice.</summary>
    internal void End()
    {
        lock (_gate)
        {
            if (_ended) return;
            _ended = true;
        }
        _available.Release();
    }

    /// <summary>Whether the body has been ended, by the test or by the client letting go of it.</summary>
    internal bool Ended
    {
        get { lock (_gate) return _ended; }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            lock (_gate)
            {
                if (_offset < _current.Length)
                {
                    var taken = Math.Min(buffer.Length, _current.Length - _offset);
                    _current.AsSpan(_offset, taken).CopyTo(buffer.Span);
                    _offset += taken;
                    return taken;
                }
                if (_chunks.Count > 0)
                {
                    _current = _chunks.Dequeue();
                    _offset = 0;
                    continue;
                }
                if (_ended) return 0;
            }
            await _available.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Ends the body, which is all closing it means here.
    /// </summary>
    /// <remarks>
    /// Called more than once by design: <see cref="StreamContent"/> disposes the body when the response
    /// is disposed, and the server disposes it again when the test is over. The semaphore is
    /// deliberately left undisposed — it has no wait handle, so it does not need to be, and disposing
    /// it would make the second close throw while the first was still being read.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing) End();
        base.Dispose(disposing);
    }
}
