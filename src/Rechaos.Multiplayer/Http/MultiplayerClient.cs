using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

/// <summary>Where a client is pointed and how patiently it waits.</summary>
/// <param name="BaseAddress">Server origin, e.g. <c>https://play.example.org</c>.</param>
/// <param name="RequestTimeout">
/// Abandons a request that has produced nothing for this long. Event streams are exempt: they are
/// expected to stay open for the length of a match and carry their own keepalives.
/// </param>
public sealed record MultiplayerClientOptions(Uri BaseAddress, TimeSpan? RequestTimeout = null)
{
    /// <summary>Fifteen seconds: long enough for a cold self-hosted server, short enough to notice.</summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// What to set <see cref="HttpClient.MaxResponseContentBufferSize"/> to on a client that talks
    /// to a coordination server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default is two gigabytes, which is to say no bound: a malicious or compromised custom
    /// server can answer any call with a body that large and the client buffers all of it before any
    /// parser sees a byte. The frame and save limits downstream are checked after the buffering, so
    /// they do not help.
    /// </para>
    /// <para>
    /// Eight megabytes covers the largest answer a server legitimately gives with room to spare: a
    /// snapshot is a megabyte of base64, and a sealed set is six players at a quarter of a megabyte
    /// of orders each. Event streams are unaffected — they are read with
    /// <see cref="HttpCompletionOption.ResponseHeadersRead"/>, which does not buffer.
    /// </para>
    /// </remarks>
    public const long MaximumResponseBytes = 8L * 1024 * 1024;

    /// <summary>
    /// How long a pooled connection may be reused before it is replaced.
    /// </summary>
    /// <remarks>
    /// The default is forever, which is right against socket exhaustion and wrong against a central
    /// service behind an edge whose addresses rotate: a pooled connection to an address that has
    /// gone away is not refused, it simply never answers, so every call through it costs a full
    /// request deadline until the operating system resets the socket. Recycling on a few minutes
    /// means DNS is re-read that often and a dead address is dropped with the connection that used
    /// it. Connections already carrying a request — an event stream, most of all — are not cut off;
    /// the lifetime governs reuse, not the connection in hand.
    /// </remarks>
    public static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(5);

    /// <summary>A client bounded by <see cref="MaximumResponseBytes"/>, for a caller that owns one.</summary>
    public static HttpClient CreateHttpClient() =>
        new(new SocketsHttpHandler { PooledConnectionLifetime = PooledConnectionLifetime })
        {
            MaxResponseContentBufferSize = MaximumResponseBytes,
        };

    internal TimeSpan EffectiveTimeout => RequestTimeout ?? DefaultRequestTimeout;

    /// <summary>
    /// <see cref="BaseAddress"/> guaranteed to end in a slash, so routes resolve beneath it.
    /// </summary>
    /// <remarks>
    /// <c>new Uri(base, relative)</c> replaces the last segment of a base that does not end in one,
    /// so <c>https://host/game</c> would lose <c>/game</c> and a server published under a path
    /// would be unreachable. A player typing the address has no reason to add the slash.
    /// </remarks>
    internal Uri RootAddress { get; } = WithTrailingSlash(BaseAddress);

    internal static Uri WithTrailingSlash(Uri address) =>
        address.AbsolutePath.EndsWith('/') ? address : new Uri(address, $"{address.AbsolutePath}/");
}

/// <summary>
/// Typed access to the coordination server: the unauthenticated doors, plus a token-bound handle
/// for everything else.
/// </summary>
/// <remarks>
/// Identity is a capability token. Creating or joining answers one, it is scoped to one player in
/// one match, and being kicked revokes it — so a 401 on a later call means the
/// membership is gone, not that a credential expired.
/// </remarks>
/// <example>
/// <code>
/// using var http = new HttpClient();
/// var client = new MultiplayerClient(http, new MultiplayerClientOptions(new Uri("http://localhost:8787")));
/// var membership = await client.CreateMatchAsync(request, cancellationToken);
/// var match = client.WithToken(membership.Token).Match(membership.Match.Id);
/// </code>
/// </example>
public sealed class MultiplayerClient
{
    private const int MaximumStreamErrorBytes = 64 * 1024;
    private readonly HttpClient _http;
    private readonly MultiplayerClientOptions _options;
    private readonly string? _token;
    private readonly HandshakeState _handshake;

    public MultiplayerClient(HttpClient http, MultiplayerClientOptions options)
        : this(http, options, token: null, new HandshakeState())
    {
    }

    private MultiplayerClient(
        HttpClient http,
        MultiplayerClientOptions options,
        string? token,
        HandshakeState handshake)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _token = token;
        _handshake = handshake;
    }

    /// <summary>The same client, sending a player's bearer token on every call.</summary>
    public MultiplayerClient WithToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new MultiplayerClient(_http, _options, token, _handshake);
    }

    /// <summary>Public lobbies, when the server enables listing.</summary>
    public Task<LobbyList> ListLobbiesAsync(CancellationToken cancellationToken) =>
        SendAsync<LobbyList>(HttpMethod.Get, ApiRoutes.Matches, body: null, cancellationToken);

    /// <summary>Opens a lobby and takes its host seat.</summary>
    public Task<MembershipView> CreateMatchAsync(
        CreateMatchRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<MembershipView>(HttpMethod.Post, ApiRoutes.Matches, request, cancellationToken);

    /// <summary>Claims a seat by join code.</summary>
    public Task<MembershipView> JoinAsync(
        JoinMatchRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<MembershipView>(HttpMethod.Post, ApiRoutes.JoinMatch, request, cancellationToken);

    public Task<MembershipView> JoinRunningAsync(
        JoinRunningMatchRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<MembershipView>(
            HttpMethod.Post, ApiRoutes.JoinRunningMatch, request, cancellationToken);

    /// <summary>A handle for the calls that name a match.</summary>
    public MatchHandle Match(string matchId) => new(this, matchId);

    /// <summary>
    /// One request, with the answer read as <typeparamref name="T"/>.
    /// </summary>
    /// <param name="exactRoundTrip">
    /// Refuse a field this build does not know about, rather than skipping it. Set for a payload
    /// whose digest this client recomputes: there, a field dropped on the way in is a hash that
    /// cannot match, and saying which field is missing beats reporting a mismatch. Everything else
    /// tolerates a server that has grown one — see <see cref="WireJson.Read{T}"/>.
    /// </param>
    internal async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken,
        bool exactRoundTrip = false)
    {
        await EnsureHandshakeAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(method, Absolute(path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        Authorize(request);
        if (body is not null)
        {
            request.Content = new StringContent(
                WireJson.Write(body), Encoding.UTF8, "application/json");
        }

        using var timeout = Deadline(cancellationToken);
        using var response = await SendWithDeadlineAsync(
            request, HttpCompletionOption.ResponseContentRead, timeout, cancellationToken)
            .ConfigureAwait(false);
        _handshake.ObserveServerDate(response.Headers.Date);
        if (!response.IsSuccessStatusCode)
        {
            throw await MultiplayerApiException
                .FromResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
        if (typeof(T) == typeof(Unit)) return (T)(object)Unit.Value;
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            throw new MultiplayerProtocolException(
                $"the server answered 204 where a {typeof(T).Name} was expected");
        }
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return exactRoundTrip ? WireJson.ReadExact<T>(payload) : WireJson.Read<T>(payload);
    }

    /// <summary>
    /// Sends a request, telling its own deadline apart from the caller's cancellation.
    /// </summary>
    /// <remarks>
    /// Both arrive from the HTTP stack as an <see cref="OperationCanceledException"/> and they mean
    /// opposite things: a deadline is worth another attempt, and a caller that asked to stop must
    /// never be retried. Only the linked source can tell them apart, and only here, so this is
    /// where the distinction is made — see <see cref="MultiplayerTimeoutException"/>.
    /// </remarks>
    private async Task<HttpResponseMessage> SendWithDeadlineAsync(
        HttpRequestMessage request,
        HttpCompletionOption completion,
        CancellationTokenSource timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, completion, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            throw new MultiplayerTimeoutException(_options.EffectiveTimeout, exception);
        }
    }

    /// <summary>
    /// Opens the event stream. The caller owns the response and disposes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It reads with <see cref="HttpCompletionOption.ResponseHeadersRead"/>, so the BODY is outside
    /// the request deadline: a stream that has said nothing for a minute is healthy, and buffering
    /// it to completion would mean never seeing an event at all.
    /// </para>
    /// <para>
    /// The HEADER phase keeps the ordinary deadline. Without one the only bound was
    /// <see cref="HttpClient.Timeout"/>, a hundred seconds on the shared client, and it surfaces as
    /// a <see cref="TaskCanceledException"/> the retry policy reads as fatal — so a reconnect
    /// through a captive network that never answers ended the match instead of spending the
    /// five-minute reconnect window the docs promise.
    /// </para>
    /// </remarks>
    internal async Task<HttpResponseMessage> OpenStreamAsync(
        string path,
        int afterSeq,
        CancellationToken cancellationToken)
    {
        await EnsureHandshakeAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, Absolute(path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("Last-Event-ID", afterSeq.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Authorize(request);
        // Disposed once the headers are in, never before: tying the body to it would cancel the
        // stream fifteen seconds after it opened.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.EffectiveTimeout);
        var response = await SendWithDeadlineAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeout, cancellationToken)
            .ConfigureAwait(false);
        _handshake.ObserveServerDate(response.Headers.Date);
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            try
            {
                throw await MultiplayerApiException
                    .FromResponseAsync(response, timeout.Token, MaximumStreamErrorBytes)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                throw new MultiplayerTimeoutException(_options.EffectiveTimeout, exception);
            }
        }
    }

    /// <summary>How far the server's clock is ahead of this machine's; see `HandshakeState`.</summary>
    public TimeSpan ServerTimeOffset => _handshake.ServerOffset;

    /// <summary>
    /// Forgets the handshake, so the next call establishes the protocol again.
    /// </summary>
    /// <remarks>
    /// The handshake used to be taken once and never revisited, which is right for a session that
    /// begins and ends inside one server version and wrong across a redeploy: the stream reconnects
    /// (a 5xx or a reset during a restart is transient), and every call after it is made under a
    /// contract the other side may no longer speak. That surfaces as a schema refusal on some later
    /// request, an unreadable event, or — worst — a tolerant read of a field whose meaning changed,
    /// where "Update your game to connect to this server" was the truth all along. The stream asks
    /// for this whenever it opens a fresh connection, which is exactly when a redeploy would have
    /// happened underneath it.
    /// </remarks>
    public void ForgetHandshake() => _handshake.Complete = false;

    /// <summary>
    /// The absolute URL of a route, under whatever path the server is mounted at.
    /// </summary>
    /// <remarks>
    /// The prefix is joined as a relative reference against a base that always ends in a slash,
    /// because an absolute one (<c>/api/v1/...</c>) resolves from the host root and would discard
    /// the path of a server published behind a reverse proxy at <c>https://host/game/</c>.
    /// </remarks>
    private Uri Absolute(string path) =>
        new(_options.RootAddress, $"{ApiRoutes.Prefix}{path}".TrimStart('/'));

    private void Authorize(HttpRequestMessage request)
    {
        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    private async Task EnsureHandshakeAsync(CancellationToken cancellationToken)
    {
        if (_handshake.Complete) return;
        await _handshake.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_handshake.Complete) return;
            using var request = new HttpRequestMessage(HttpMethod.Post, Absolute(ApiRoutes.Handshake));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(
                WireJson.Write(new HandshakeRequest(MultiplayerProtocolVersion.Current)),
                Encoding.UTF8,
                "application/json");
            using var timeout = Deadline(cancellationToken);
            using var response = await SendWithDeadlineAsync(
                request, HttpCompletionOption.ResponseContentRead, timeout, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw await MultiplayerApiException
                    .FromResponseAsync(response, cancellationToken).ConfigureAwait(false);
            }
            var payload = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            var handshake = WireJson.Read<HandshakeResponse>(payload);
            if (handshake.ProtocolVersion != MultiplayerProtocolVersion.Current)
            {
                var action = MultiplayerProtocolVersion.Current < handshake.ProtocolVersion
                    ? "Update your game to connect to this server."
                    : "This server is outdated and needs an update.";
                throw new MultiplayerProtocolException(
                    $"protocol version mismatch: client version {MultiplayerProtocolVersion.Current}, "
                    + $"server version {handshake.ProtocolVersion}. {action}");
            }
            _handshake.Complete = true;
        }
        finally
        {
            _handshake.Gate.Release();
        }
    }

    private CancellationTokenSource Deadline(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeout = _options.EffectiveTimeout;
        if (timeout > TimeSpan.Zero) source.CancelAfter(timeout);
        return source;
    }
}

/// <summary>
/// What the client has established about the server it is talking to, shared by every handle.
/// </summary>
/// <remarks>
/// Both facts here are about the server rather than about a seat, which is why they outlive the
/// token-bound handles that are derived from this client: the protocol both sides speak, and how
/// far the server's clock is from this machine's.
/// </remarks>
internal sealed class HandshakeState
{
    private long _offsetTicks;

    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal volatile bool Complete;

    /// <summary>
    /// How far the server's clock is ahead of this one, smoothed over the responses seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every deadline the protocol carries is an instant on the SERVER's clock, and the countdown a
    /// player watches is that instant minus the time on theirs. A machine thirty seconds fast on a
    /// thirty-second timer shows the turn expiring before the server seals it; one that is slow is
    /// sealed on while the screen still shows time to plan. Neither is a protocol failure and
    /// neither is rare — an unsynchronised clock is an ordinary thing for a desktop to have.
    /// </para>
    /// <para>
    /// The server is still the authority on when a turn ends. This only makes the courtesy
    /// countdown honest, so it is deliberately cheap: the `Date` header every HTTP response
    /// already carries, smoothed so that one slow response cannot jerk the clock on screen.
    /// </para>
    /// </remarks>
    internal TimeSpan ServerOffset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    /// <summary>
    /// Folds one response's <c>Date</c> into the offset.
    /// </summary>
    /// <remarks>
    /// The header has one-second resolution and the response spent some of the round trip in
    /// flight, so a single reading is worth little; the exponential average over many is worth
    /// enough for a countdown. The first reading is taken whole, because starting from zero would
    /// mean the first turn of a session is shown on an uncorrected clock.
    /// </remarks>
    internal void ObserveServerDate(DateTimeOffset? served)
    {
        if (served is not { } instant) return;
        var sample = (instant - DateTimeOffset.UtcNow).Ticks;
        var previous = Interlocked.Read(ref _offsetTicks);
        var blended = previous == 0 ? sample : previous + ((sample - previous) / 4);
        Interlocked.Exchange(ref _offsetTicks, blended);
    }
}

/// <summary>The absence of a body, for the calls that answer 204.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
