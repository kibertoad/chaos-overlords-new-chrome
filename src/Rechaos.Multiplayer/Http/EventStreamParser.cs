using System.Globalization;
using System.Text;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

/// <summary>
/// One frame off the event stream: an event, or a keepalive that carried none.
/// </summary>
/// <remarks>
/// Keepalives are surfaced rather than swallowed because they are the proof that a connection is
/// alive. A reader that only ever saw events could not tell a healthy stream between turns from a
/// socket the network has quietly forgotten about.
/// </remarks>
public readonly record struct EventStreamFrame(MatchEvent? Event)
{
    public static EventStreamFrame Keepalive => default;

    public bool IsKeepalive => Event is null;
}

/// <summary>The stream went quiet for longer than the server's heartbeat allows.</summary>
/// <remarks>
/// An <see cref="IOException"/> so that the reconnect loop treats it like any other dropped
/// connection: resume from the last sequence seen, rather than end the match.
/// </remarks>
public sealed class EventStreamIdleException(TimeSpan idle, Exception? inner = null)
    : IOException($"the event stream carried nothing for {idle.TotalSeconds:0.#}s", inner)
{
    public TimeSpan Idle { get; } = idle;
}

/// <summary>
/// Reads server-sent events off a response body into match events.
/// </summary>
/// <remarks>
/// Frames are separated by a blank line; <c>data:</c> carries the JSON event, <c>id:</c> its
/// sequence number and <c>event:</c> its name. Comment lines (the keepalive) end a frame with
/// nothing in it. Either line ending is allowed, as the event-stream format says, and a byte order
/// mark in front of the first line is skipped rather than read as the start of a field name.
/// </remarks>
public static class EventStreamParser
{
    /// <summary>
    /// The one event name the stream contract declares, mirroring <c>MATCH_EVENT_SSE_NAME</c> in
    /// <c>@chaos-overlords/contracts</c>.
    /// </summary>
    /// <remarks>
    /// Every frame carries the same envelope whatever its <c>type</c>, so the stream is one named
    /// event rather than one per match event type, and <c>message</c> is the name a stock
    /// <c>EventSource</c> delivers to its default handler.
    /// </remarks>
    internal const string EventName = "message";

    /// <summary>Match events from one connection's body, until the server closes it.</summary>
    public static async IAsyncEnumerable<MatchEvent> ReadAsync(
        Stream body,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (var frame in ReadFramesAsync(body, idleTimeout: null, cancellationToken)
            .ConfigureAwait(false))
        {
            if (frame.Event is { } @event) yield return @event;
        }
    }

    /// <summary>
    /// Every frame from one connection's body, keepalives included, until the server closes it.
    /// </summary>
    /// <param name="body">The response body, read line by line and never buffered whole.</param>
    /// <param name="idleTimeout">
    /// How long the body may carry nothing before the connection is given up on as dead, or null
    /// to wait forever. The server heartbeats on a fixed interval, so silence well past it means the
    /// socket is gone even though nothing has said so.
    /// </param>
    /// <param name="cancellationToken">The caller's own stop, which is never reported as idleness.</param>
    /// <exception cref="EventStreamIdleException">Nothing arrived within <paramref name="idleTimeout"/>.</exception>
    public static async IAsyncEnumerable<EventStreamFrame> ReadFramesAsync(
        Stream body,
        TimeSpan? idleTimeout,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        using var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var lines = new BoundedLineReader(reader, MaximumFrameChars);
        // One linked source, re-armed after every line: `CancelAfter` restarts the countdown, so
        // the deadline is always measured from the last byte rather than from the connection.
        using var idle = idleTimeout is { } ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null;
        var frame = new StringBuilder();
        var firstLine = true;
        while (true)
        {
            var line = await ReadLineAsync(lines, idle, idleTimeout, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                // The connection ended. A frame with no blank line after it was cut mid-write, and
                // delivering half an event is worse than resuming from the last complete one.
                yield break;
            }
            if (firstLine)
            {
                firstLine = false;
                if (line.Length > 0 && line[0] == '﻿') line = line[1..];
            }
            if (line.Length > 0)
            {
                if (frame.Length + line.Length > MaximumFrameChars)
                {
                    // A frame this long is not an event. Without a ceiling, a server that never sent
                    // the blank line would grow this buffer for as long as it kept writing.
                    throw new MultiplayerProtocolException(
                        $"an event stream frame passed {MaximumFrameChars} characters without ending");
                }
                frame.Append(line).Append('\n');
                continue;
            }
            var parsed = ParseFrame(frame.ToString());
            frame.Clear();
            yield return new EventStreamFrame(parsed);
        }
    }

    /// <summary>One line, with the idle deadline told apart from the caller's cancellation.</summary>
    private static async Task<string?> ReadLineAsync(
        BoundedLineReader reader,
        CancellationTokenSource? idle,
        TimeSpan? idleTimeout,
        CancellationToken cancellationToken)
    {
        if (idle is null) return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        idle.CancelAfter(idleTimeout!.Value);
        try
        {
            return await reader.ReadLineAsync(idle.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EventStreamIdleException(idleTimeout.Value, exception);
        }
    }

    /// <summary>
    /// Reads lines in chunks, refusing one longer than the frame limit.
    /// </summary>
    /// <remarks>
    /// <see cref="StreamReader.ReadLineAsync(CancellationToken)"/> grows its own buffer until it
    /// finds a newline, so the frame ceiling the caller applies afterwards bounded nothing: a
    /// server writing bytes with no newline could deliver gigabytes into this process first, and
    /// the idle timer is re-armed per line rather than per byte, so it does not help either.
    /// <see cref="HttpClient.MaxResponseContentBufferSize"/> does not apply to a
    /// <see cref="HttpCompletionOption.ResponseHeadersRead"/> body.
    /// </remarks>
    private sealed class BoundedLineReader(StreamReader reader, int maximumLineChars)
    {
        private readonly char[] _buffer = new char[8192];
        private readonly StringBuilder _line = new();
        private int _start;
        private int _length;
        private bool _skipLeadingNewline;

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                while (_start < _length)
                {
                    var character = _buffer[_start++];
                    if (_skipLeadingNewline)
                    {
                        _skipLeadingNewline = false;
                        if (character == '\n') continue;
                    }
                    if (character == '\r')
                    {
                        // CRLF and a bare CR both end a line, as the event-stream format says.
                        _skipLeadingNewline = true;
                        return Take();
                    }
                    if (character == '\n') return Take();
                    if (_line.Length >= maximumLineChars)
                    {
                        throw new MultiplayerProtocolException(
                            $"an event stream line passed {maximumLineChars} characters without ending");
                    }
                    _line.Append(character);
                }
                _start = 0;
                _length = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                if (_length != 0) continue;
                // End of stream. A trailing partial line is dropped: the caller treats an
                // unterminated frame as one that was cut mid-write.
                return null;
            }
        }

        private string Take()
        {
            var line = _line.ToString();
            _line.Clear();
            return line;
        }
    }

    /// <summary>
    /// The largest frame worth assembling.
    /// </summary>
    /// <remarks>
    /// Generous next to the events the protocol defines — the longest carries a handful of state
    /// hashes — and far below the snapshots, which are fetched over REST rather than streamed. It is
    /// here to bound the buffer, not to validate an event.
    /// </remarks>
    private const int MaximumFrameChars = 256 * 1024;

    /// <summary>
    /// One frame, or null when it carried no data (a keepalive comment).
    /// </summary>
    /// <remarks>
    /// The <c>id:</c> field is reconciled with the <c>seq</c> inside the JSON rather than ignored.
    /// Both are written from the same number, so a disagreement means the frame was mangled in
    /// transit or the server is not the one this client thinks it is. Resuming from the wrong
    /// number would skip events in silence, so the frame is refused instead — and so is an id that
    /// is not a number at all, for the same reason.
    /// <para>
    /// The <c>event:</c> name is held to <see cref="EventName"/> the same way. The handshake has
    /// already established that both sides speak the same protocol version, so a frame under any
    /// other name is a mangled or foreign one rather than a newer server being polite. An absent
    /// name means <c>message</c>, as the event-stream format says.
    /// </para>
    /// </remarks>
    internal static MatchEvent? ParseFrame(string frame)
    {
        var data = new StringBuilder();
        string? id = null;
        string? name = null;
        var sawData = false;
        foreach (var line in frame.Split('\n'))
        {
            if (line.Length == 0 || line[0] == ':') continue;
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (sawData) data.Append('\n');
                data.Append(line.AsSpan(5).TrimStart());
                sawData = true;
            }
            else if (line.StartsWith("id:", StringComparison.Ordinal))
            {
                id = line.AsSpan(3).Trim().ToString();
            }
            else if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                name = line.AsSpan(6).Trim().ToString();
            }
        }
        if (!sawData) return null;
        if (name is not null && !string.Equals(name, EventName, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"event stream frame is named '{name}', not the contracted '{EventName}'");
        }

        var @event = WireJson.Read<MatchEvent>(data.ToString());
        // An empty id is the format's way of saying "no id", and is left alone.
        if (string.IsNullOrEmpty(id)) return @event;
        if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var framed))
        {
            throw new MultiplayerProtocolException(
                $"event stream frame id '{id}' is not a sequence number");
        }
        if (framed != @event.Seq)
        {
            throw new MultiplayerProtocolException(
                $"event stream frame id {framed} disagrees with its payload seq {@event.Seq}");
        }
        return @event;
    }
}
