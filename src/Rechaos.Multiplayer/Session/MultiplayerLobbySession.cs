using System.Collections.Concurrent;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>Something the lobby wants the interface to know about, drained from the game loop.</summary>
/// <remarks>
/// The same shape as <see cref="MultiplayerNotice"/> and for the same reason: a lobby call is a round
/// trip, a round trip cannot happen on the thread that draws, and an answer is only safe to act on
/// where the interface's own state lives.
/// </remarks>
public abstract record LobbyNotice
{
    private LobbyNotice()
    {
    }

    /// <summary>A seat was taken: this is the membership and the token that proves it.</summary>
    public sealed record Seated(MembershipView Membership) : LobbyNotice;

    /// <summary>The lobby as the server now describes it.</summary>
    public sealed record Updated(MatchView Match) : LobbyNotice;

    /// <summary>The seat was given up, successfully or not; either way it is gone.</summary>
    public sealed record Left : LobbyNotice;

    /// <summary>A call was refused, with text a player can act on.</summary>
    public sealed record Failed(string Reason) : LobbyNotice;
}

/// <summary>
/// The lobby half of an online match: taking a seat, watching who else arrives, and starting.
/// </summary>
/// <remarks>
/// <para>
/// Every call runs on a background task and answers through <see cref="TryDequeueNotice"/>, because
/// the caller is a game loop. Blocking it on a round trip freezes the window for as long as the
/// server takes to answer — up to the request deadline — and a lobby is polled once a second, so the
/// freeze would not even be a one-off.
/// </para>
/// <para>
/// One call at a time. A second request while one is in flight is dropped rather than queued: the
/// operations here are a player pressing a button and a poll that will come round again, and neither
/// is worth remembering twice. <see cref="IsBusy"/> is what the interface disables its buttons on.
/// </para>
/// <para>
/// The server address is settled once, when the session is constructed. A handle built per call from
/// a text field the player can still edit would be a different server halfway through a lobby.
/// </para>
/// </remarks>
public sealed class MultiplayerLobbySession : IAsyncDisposable
{
    private readonly MultiplayerClient _anonymous;
    private readonly ConcurrentQueue<LobbyNotice> _notices = new();
    private readonly CancellationTokenSource _stopping = new();
    private MatchHandle? _handle;
    private Task _current = Task.CompletedTask;
    private int _busy;

    /// <summary>A session pointed at one server.</summary>
    /// <param name="http">Shared by every call; the game owns it.</param>
    /// <param name="options">Where the server is, and how patiently to wait for it.</param>
    public MultiplayerLobbySession(HttpClient http, MultiplayerClientOptions options)
    {
        _anonymous = new MultiplayerClient(http, options);
    }

    /// <summary>Whether a call is in flight, and so whether the buttons should be live.</summary>
    public bool IsBusy => Volatile.Read(ref _busy) != 0;

    /// <summary>The token-bound handle for this player's seat, once there is one.</summary>
    public MatchHandle? Handle => _handle;

    /// <summary>This client's own player id, once seated.</summary>
    public string OwnPlayerId { get; private set; } = string.Empty;

    /// <summary>The next thing the interface should know about, if anything is waiting.</summary>
    public bool TryDequeueNotice(out LobbyNotice notice) => _notices.TryDequeue(out notice!);

    /// <summary>Opens a lobby and takes its host seat.</summary>
    public void Host(CreateMatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Run(async token => Seat(await _anonymous.CreateMatchAsync(request, token).ConfigureAwait(false)));
    }

    /// <summary>Claims a seat by join code.</summary>
    public void Join(JoinMatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Run(async token => Seat(await _anonymous.JoinAsync(request, token).ConfigureAwait(false)));
    }

    /// <summary>Host only: seats the players, draws the seed and opens turn 1.</summary>
    public void Start() => Run(async token =>
    {
        if (_handle is null) return;
        await _handle.StartAsync(token).ConfigureAwait(false);
        _notices.Enqueue(new LobbyNotice.Updated(
            (await _handle.GetAsync(token).ConfigureAwait(false)).Match));
    });

    /// <summary>Re-reads the lobby, for the roster and for the moment it starts running.</summary>
    public void Refresh() => Run(async token =>
    {
        if (_handle is null) return;
        _notices.Enqueue(new LobbyNotice.Updated(
            (await _handle.GetAsync(token).ConfigureAwait(false)).Match));
    });

    /// <summary>
    /// Gives up the seat.
    /// </summary>
    /// <remarks>
    /// Best effort, and the notice says the seat is gone either way: the server stops waiting on this
    /// player whether or not the call lands, and a player who has decided to go should not be held in
    /// a lobby by a network error.
    /// </remarks>
    public void Leave() => Run(async token =>
    {
        try
        {
            if (_handle is not null) await _handle.LeaveAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _handle = null;
            _notices.Enqueue(new LobbyNotice.Left());
        }
    });

    /// <summary>
    /// Stops accepting calls and answers a task that completes when the last one has finished.
    /// </summary>
    /// <remarks>
    /// For a caller that must not block. The task owns what the session holds, so somebody has to
    /// observe it — but not the thread that asked for the stop.
    /// </remarks>
    public Task StopAsync()
    {
        _stopping.Cancel();
        return DisposeAsync().AsTask();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _current.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping mid-call is how a lobby is left.
        }
        _stopping.Dispose();
    }

    private void Seat(MembershipView membership)
    {
        OwnPlayerId = membership.Player.Id;
        _handle = _anonymous.WithToken(membership.Token).Match(membership.Match.Id);
        _notices.Enqueue(new LobbyNotice.Seated(membership));
    }

    /// <summary>
    /// Runs one operation off the caller's thread, unless one is already running.
    /// </summary>
    /// <remarks>
    /// Every failure a server or a network can produce becomes a <see cref="LobbyNotice.Failed"/>.
    /// The caller is a game loop with no <c>try</c> around it, so an exception escaping here would
    /// close the window rather than tell the player their join code was wrong.
    /// </remarks>
    private void Run(Func<CancellationToken, Task> operation)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
        _current = RunAsync(operation);
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await operation(_stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // The player left while this was in flight; there is nobody to tell.
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException or MultiplayerTimeoutException
            or HttpRequestException or IOException)
        {
            _notices.Enqueue(new LobbyNotice.Failed(Describe(exception)));
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    /// <summary>
    /// Text a player can act on, rather than an exception's own words.
    /// </summary>
    /// <remarks>
    /// The reason, not the status, is what this branches on: several refusals share a status and mean
    /// different things, and the server's message is written for a developer reading a log.
    /// </remarks>
    private static string Describe(Exception exception) => exception switch
    {
        MultiplayerApiException { Reason: "invalid_token" or "revoked" } =>
            "THAT MEMBERSHIP IS NO LONGER VALID",
        MultiplayerApiException { Reason: "unknown_match" } => "NO MATCH WITH THAT CODE",
        MultiplayerApiException { Reason: "match_full" } => "THAT LOBBY IS FULL",
        MultiplayerApiException { Reason: "host_only" } => "ONLY THE HOST CAN DO THAT",
        MultiplayerApiException { Reason: "invalid_password" } => "WRONG PASSWORD",
        MultiplayerApiException { Reason: "match_started" } => "THAT MATCH HAS ALREADY STARTED",
        MultiplayerApiException { Reason: "reserved_display_name" } =>
            "THAT NAME IS A CHEAT CODE  PICK ANOTHER",
        MultiplayerApiException api => api.Message.ToUpperInvariant(),
        MultiplayerTimeoutException => "THE SERVER DID NOT ANSWER",
        MultiplayerProtocolException protocol => protocol.Message.ToUpperInvariant(),
        _ => "COULD NOT REACH THE SERVER",
    };
}
