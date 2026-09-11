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

    internal TimeSpan EffectiveTimeout => RequestTimeout ?? DefaultRequestTimeout;

    /// <summary>
    /// <see cref="BaseAddress"/> guaranteed to end in a slash, so routes resolve beneath it.
    /// </summary>
    /// <remarks>
    /// <c>new Uri(base, relative)</c> replaces the last segment of a base that does not end in one,
    /// so <c>https://host/game</c> would lose <c>/game</c> and a server published under a path
    /// would be unreachable. A player typing the address has no reason to add the slash.
    /// </remarks>
    internal Uri RootAddress { get; } = BaseAddress.AbsolutePath.EndsWith('/')
        ? BaseAddress
        : new Uri(BaseAddress, $"{BaseAddress.AbsolutePath}/");
}

/// <summary>
/// Typed access to the coordination server: the unauthenticated doors, plus a token-bound handle
/// for everything else.
/// </summary>
/// <remarks>
/// Identity is a capability token. Creating or joining answers one, it is scoped to one player in
/// one match, and leaving or being kicked revokes it — so a 401 on a later call means the
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
    private readonly HttpClient _http;
    private readonly MultiplayerClientOptions _options;
    private readonly string? _token;

    public MultiplayerClient(HttpClient http, MultiplayerClientOptions options)
        : this(http, options, token: null)
    {
    }

    private MultiplayerClient(HttpClient http, MultiplayerClientOptions options, string? token)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _token = token;
    }

    /// <summary>The same client, sending a player's bearer token on every call.</summary>
    public MultiplayerClient WithToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new MultiplayerClient(_http, _options, token);
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
    /// It reads with <see cref="HttpCompletionOption.ResponseHeadersRead"/> and outside the request
    /// deadline: a stream that has said nothing for a minute is healthy, and buffering it to
    /// completion would mean never seeing an event at all.
    /// </remarks>
    internal async Task<HttpResponseMessage> OpenStreamAsync(
        string path,
        int afterSeq,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Absolute(path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("Last-Event-ID", afterSeq.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Authorize(request);
        var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            throw await MultiplayerApiException
                .FromResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

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

    private CancellationTokenSource Deadline(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeout = _options.EffectiveTimeout;
        if (timeout > TimeSpan.Zero) source.CancelAfter(timeout);
        return source;
    }
}

/// <summary>The absence of a body, for the calls that answer 204.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
