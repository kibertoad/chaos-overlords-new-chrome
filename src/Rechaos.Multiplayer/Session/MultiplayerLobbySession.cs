using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
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

    public sealed record Listed(IReadOnlyList<LobbyListing> Matches) : LobbyNotice;

    /// <summary>A call was refused, with text a player can act on.</summary>
    public sealed record Failed(string Reason, Exception? Error = null, string? Operation = null) : LobbyNotice;

    /// <summary>Chat messages the lobby's log gained, oldest first.</summary>
    public sealed record Chatted(IReadOnlyList<LobbyChatLine> Lines) : LobbyNotice;
}

/// <summary>One lobby chat message, as the event log carries it.</summary>
/// <param name="Seq">The event's sequence number, unique within the match.</param>
/// <param name="PlayerId">Who posted it.</param>
/// <param name="Text">What they wrote, as the server normalised it.</param>
public sealed record LobbyChatLine(int Seq, string PlayerId, string Text);

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
    private readonly Lock _disposalGate = new();
    private readonly Lock _seatGate = new();
    private MatchHandle? _handle;
    private Task _current = Task.CompletedTask;
    private Task _leaving = Task.CompletedTask;
    private Task? _disposal;
    private int _busy;
    private int _leaveGeneration;
    private bool _stopped;

    /// <summary>The last event of this lobby's log the chat has read through.</summary>
    /// <remarks>Only the polling call touches it, and only one call runs at a time.</remarks>
    private int _chatCursor;

    /// <summary>The chat messages still being sent, in the order they were written.</summary>
    private Task _chatTail = Task.CompletedTask;
    private readonly Lock _chatGate = new();

    /// <summary>A session pointed at one server.</summary>
    /// <param name="http">Shared by every call; the game owns it.</param>
    /// <param name="options">Where the server is, and how patiently to wait for it.</param>
    public MultiplayerLobbySession(HttpClient http, MultiplayerClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _anonymous = new MultiplayerClient(http, options);
        BaseAddress = options.RootAddress;
    }

    /// <summary>
    /// The server this session actually dials.
    /// </summary>
    /// <remarks>
    /// Recorded here because the connect form is not it. A recovery record written from the address
    /// the form was showing, after a retry succeeded against the one the session had kept, names a
    /// server the seat is not on, and every later reconnect for it answers 401 or 404.
    /// </remarks>
    public Uri BaseAddress { get; }

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
        Claim(token => _anonymous.CreateMatchAsync(request, token));
    }

    /// <summary>Claims a seat by join code.</summary>
    public void Join(JoinMatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Claim(token => _anonymous.JoinAsync(request, token));
    }

    public void JoinRunning(JoinRunningMatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Claim(token => _anonymous.JoinRunningAsync(request, token));
    }

    /// <summary>Asks the server for a new seat, and gives it back if nobody is left to sit in it.</summary>
    /// <remarks>
    /// A claim commits at the server whether or not its answer gets back, so it is deliberately not
    /// bound to this session's cancellation: cutting it off on stop would leave a seat taken that no
    /// recovery record names. The client's own request deadline bounds it instead, and
    /// <see cref="SeatAsync"/> releases whatever it returns after a leave or a stop.
    /// </remarks>
    private void Claim(
        Func<CancellationToken, Task<MembershipView>> request,
        [CallerMemberName] string operationName = "")
    {
        var generation = Volatile.Read(ref _leaveGeneration);
        Run(async _ => await SeatAsync(
                await request(CancellationToken.None).ConfigureAwait(false), generation, claimed: true)
            .ConfigureAwait(false), operationName);
    }

    public void Browse() => Run(async token =>
        _notices.Enqueue(new LobbyNotice.Listed(
            (await _anonymous.ListLobbiesAsync(token).ConfigureAwait(false)).Matches)));

    /// <summary>Reclaims an existing seat after restarting with its durable membership token.</summary>
    public void Resume(string matchId, string playerId, string token, string joinCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(playerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var handle = _anonymous.WithToken(token).Match(matchId);
        var generation = Volatile.Read(ref _leaveGeneration);
        // Not bound to the session's cancellation either, for the same reason as a claim: a leave
        // asked for while this is in flight has to see its answer to give the seat back.
        Run(async _ =>
        {
            var cancellationToken = CancellationToken.None;
            var detail = await handle.GetAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(detail.You, playerId, StringComparison.Ordinal))
                throw new MultiplayerProtocolException("the saved membership belongs to another player");
            var player = detail.Match.Players.FirstOrDefault(candidate => candidate.Id == playerId)
                ?? throw new MultiplayerProtocolException("the saved player is no longer in this match");
            if (detail.Match.Status is MatchStatus.Running or MatchStatus.Desynced
                && player.Status != PlayerStatus.Active)
            {
                // A former member has a perfectly valid token, but is deliberately absent from the
                // active roster.  Reclaim that seat before constructing the match session.  In
                // particular, this is the normal route when everybody left a running match: the
                // server retains it and the first person back becomes its host.
                // Rejoining is a write, so a player who has already left or stopped must not be put
                // back into the roster (and possibly made host) only to be taken out again.
                if (IsAbandoned(generation)) return;
                await handle.RejoinAsync(cancellationToken).ConfigureAwait(false);
                detail = await handle.GetAsync(cancellationToken).ConfigureAwait(false);
                player = detail.Match.Players.First(candidate => candidate.Id == playerId);
            }
            await SeatAsync(new MembershipView(
                detail.Match, player, token, string.IsNullOrWhiteSpace(detail.JoinCode)
                    ? joinCode
                    : detail.JoinCode), generation, claimed: false).ConfigureAwait(false);
        });
    }

    /// <summary>Host only: seats the players, draws the seed and opens turn 1.</summary>
    public void Start() => Run(async token =>
    {
        // Read once: a leave clears the field from another thread while this is in flight.
        if (_handle is not { } handle) return;
        try
        {
            await handle.StartAsync(token).ConfigureAwait(false);
        }
        catch (MultiplayerApiException exception) when (exception.Status == HttpStatusCode.Conflict)
        {
            // Starting is a compare-and-swap at the server. The request can lose its response or
            // race another request from this same host, even though the match did start. The match
            // read is authoritative: treat that state as success rather than leaving the host on a
            // misleading error solely because the original transition was no longer available.
            var detail = await handle.GetAsync(token).ConfigureAwait(false);
            if (detail.Match.Seed is null || !MultiplayerMatchSession.HasFinishedStarting(detail.Match))
                throw;
            _notices.Enqueue(new LobbyNotice.Updated(detail.Match));
            return;
        }
        await PublishLobbyAsync(handle, token).ConfigureAwait(false);
    });

    public void UpdateSettings(MatchSettings settings) => Run(async token =>
    {
        if (_handle is not { } handle) return;
        await handle.UpdateSettingsAsync(settings, token).ConfigureAwait(false);
        await PublishLobbyAsync(handle, token).ConfigureAwait(false);
    });

    /// <summary>Changes this player's own name and portrait while the lobby has not started.</summary>
    public void UpdateProfile(UpdatePlayerProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Run(async token =>
        {
            if (_handle is not { } handle) return;
            await handle.UpdateProfileAsync(request, token).ConfigureAwait(false);
            await PublishLobbyAsync(handle, token).ConfigureAwait(false);
        });
    }

    /// <summary>Re-reads the lobby, for the roster and for the moment it starts running.</summary>
    public void Refresh() => Run(async token =>
    {
        if (_handle is not { } handle) return;
        await PublishLobbyAsync(handle, token).ConfigureAwait(false);
    });

    private async Task PublishLobbyAsync(MatchHandle handle, CancellationToken token)
    {
        var match = (await handle.GetAsync(token).ConfigureAwait(false)).Match;
        _notices.Enqueue(new LobbyNotice.Updated(match));
        if (match.Status != MatchStatus.Lobby || match.LastEventSeq <= _chatCursor) return;
        try
        {
            await ReadChatAsync(handle, match.LastEventSeq, token).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsServerOrNetworkFailure(exception))
        {
            // The call this read follows has already succeeded, and a profile change reported as
            // failed would be drawn as refused. The cursor stops at the last page announced, so
            // the next poll reads the rest.
        }
    }

    /// <summary>
    /// Reads the lobby's log from where the chat last stopped, and announces the messages in it.
    /// </summary>
    /// <remarks>
    /// Asked only when the view says the log has grown, so a quiet lobby costs its poll nothing
    /// more. The first read after taking a seat starts from the beginning, which is how a player
    /// who arrives late sees what was said before them. The server refuses chat once the match
    /// starts, so nothing past the lobby is ever read for it.
    /// </remarks>
    private async Task ReadChatAsync(MatchHandle handle, int throughSeq, CancellationToken token)
    {
        while (_chatCursor < throughSeq)
        {
            var page = await handle.EventsAsync(_chatCursor, ChatPageSize, token).ConfigureAwait(false);
            var before = _chatCursor;
            var lines = new List<LobbyChatLine>();
            foreach (var matchEvent in page.Events)
            {
                if (matchEvent.Seq <= _chatCursor) continue;
                _chatCursor = matchEvent.Seq;
                if (matchEvent is LobbyChatMessageEvent chat)
                    lines.Add(new LobbyChatLine(chat.Seq, chat.Payload.PlayerId, chat.Payload.Text));
            }
            // Announced page by page: the cursor has already moved past these, so a later page
            // that fails must not take them down with it.
            if (lines.Count > 0) _notices.Enqueue(new LobbyNotice.Chatted(lines));
            // A page with nothing past the cursor would be asked for again forever.
            if (_chatCursor == before) break;
        }
    }

    /// <summary>The page size a chat read asks for: the server's own ceiling.</summary>
    private const int ChatPageSize = 200;

    /// <summary>
    /// Posts a chat message to the lobby. The message comes back to every member, this one
    /// included, through the next poll's read of the log.
    /// </summary>
    /// <remarks>
    /// Outside the one-call-at-a-time rule, like <see cref="LeaveAsync"/>: the lobby is polled once
    /// a second, so a message sent under that rule would be dropped whenever it met a poll. Messages
    /// are sent one after another in the order they were written. A refusal (the rate limit, a
    /// started match) is reported like any other failed call.
    /// </remarks>
    public void SendChat(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_handle is not { } handle || _stopping.IsCancellationRequested) return;
        // Read now: a send queued behind a slow one runs after the stop may have disposed the source.
        var token = _stopping.Token;
        lock (_chatGate) _chatTail = SendChatAfterAsync(_chatTail, handle, text, token);
    }

    private async Task SendChatAfterAsync(
        Task previous, MatchHandle handle, string text, CancellationToken token)
    {
        // Each send reports its own failure; the next message is still worth sending.
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        try
        {
            await handle.PostChatAsync(new PostChatMessageRequest(text), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // The lobby is being left; there is nobody to tell.
        }
        catch (Exception exception) when (IsServerOrNetworkFailure(exception))
        {
            _notices.Enqueue(new LobbyNotice.Failed(Describe(exception), exception, nameof(SendChat)));
        }
    }

    /// <summary>
    /// Gives up the seat, and answers a task that completes when the server has been told.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately outside the one-call-at-a-time rule that governs everything else here, and
    /// outside this session's cancellation. Leaving is the one call a player can make that must not be
    /// dropped because a poll happened to be in flight, and the caller's next act is to tear the
    /// session down — so a request bound to the session's own token would be cancelled before it
    /// reached the server, and the match would keep waiting on a seat nobody is in.
    /// </para>
    /// <para>
    /// Best effort all the same: the caller should not wait on it. The seat is gone from the player's
    /// point of view the moment they ask, and the server's turn timer is what moves a match on past a
    /// client that vanished without saying so. The session does remember it, though, so that
    /// <see cref="DisposeAsync"/> lets it finish before the client it is on is torn down.
    /// </para>
    /// </remarks>
    public Task LeaveAsync()
    {
        MatchHandle? handle;
        lock (_seatGate)
        {
            _leaveGeneration++;
            handle = _handle;
            _handle = null;
            OwnPlayerId = string.Empty;
        }
        if (handle is null) return Task.CompletedTask;
        // The client's own request deadline bounds this; the session's token deliberately does not.
        var leaving = handle.LeaveAsync(CancellationToken.None);
        _leaving = leaving;
        return leaving;
    }

    /// <summary>
    /// Stops accepting calls and answers a task that completes when the last one has finished.
    /// </summary>
    /// <remarks>
    /// For a caller that must not block. The task owns what the session holds, so somebody has to
    /// observe it — but not the thread that asked for the stop. Calling either this or
    /// <see cref="DisposeAsync"/> more than once answers the same wind-down.
    /// </remarks>
    public Task StopAsync() => DisposeAsync().AsTask();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposalGate) _disposal ??= DisposeCoreAsync();
        return new ValueTask(_disposal);
    }

    private async Task DisposeCoreAsync()
    {
        // A seat call still in flight is not cancelled below; from here on it hands back whatever
        // it is given, and awaiting the current call waits for that too.
        lock (_seatGate) _stopped = true;
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _current.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping mid-call is how a lobby is left.
        }
        Task chat;
        lock (_chatGate) chat = _chatTail;
        // Every send catches its own failures, and the token cancelled above ends the one in flight.
        await chat.ConfigureAwait(false);
        try
        {
            // Bounded by the request deadline the client puts on every call, and waited for so
            // that the seat is actually given up before the caller disposes the client under it.
            await _leaving.ConfigureAwait(false);
        }
        catch (Exception exception) when (IsServerOrNetworkFailure(exception))
        {
            // A leave that did not get through is the server's turn timer's problem now.
        }
        _stopping.Dispose();
    }

    private static bool IsServerOrNetworkFailure(Exception exception) =>
        exception is MultiplayerApiException or MultiplayerProtocolException
            or MultiplayerTimeoutException or HttpRequestException or IOException;

    private bool IsAbandoned(int generation)
    {
        lock (_seatGate) return _stopped || generation != _leaveGeneration;
    }

    /// <param name="membership">What the server answered.</param>
    /// <param name="generation">The leave count when the call was asked for.</param>
    /// <param name="claimed">
    /// Whether the call took a new seat. A resumed seat was the player's before the call, and the
    /// recovery record that asked for it still names it, so a stop without a leave keeps it.
    /// </param>
    private async Task SeatAsync(MembershipView membership, int generation, bool claimed)
    {
        var handle = _anonymous.WithToken(membership.Token).Match(membership.Match.Id);
        lock (_seatGate)
        {
            var left = generation != _leaveGeneration;
            if (!left && !_stopped)
            {
                OwnPlayerId = membership.Player.Id;
                _handle = handle;
                _chatCursor = 0;
                _notices.Enqueue(new LobbyNotice.Seated(membership));
                return;
            }
            if (!left && !claimed) return;
        }
        // The server committed the request after the player left, or after a stop that means nobody
        // will ever read this answer or record its token. Release it even though nobody waits.
        try
        {
            await handle.LeaveAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsServerOrNetworkFailure(exception))
        {
            // The server's turn timer handles a seat we could not release.
        }
    }

    /// <summary>
    /// Runs one operation off the caller's thread, unless one is already running.
    /// </summary>
    /// <remarks>
    /// Every failure a server or a network can produce becomes a <see cref="LobbyNotice.Failed"/>.
    /// The caller is a game loop with no <c>try</c> around it, so an exception escaping here would
    /// close the window rather than tell the player their join code was wrong.
    /// </remarks>
    private void Run(
        Func<CancellationToken, Task> operation,
        [CallerMemberName] string operationName = "")
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
        _current = RunAsync(operation, operationName);
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation, string operationName)
    {
        try
        {
            await operation(_stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // The player left while this was in flight; there is nobody to tell.
        }
        catch (Exception exception) when (IsServerOrNetworkFailure(exception))
        {
            _notices.Enqueue(new LobbyNotice.Failed(Describe(exception), exception, operationName));
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    /// <summary>The shared map, upper-cased for this screen.</summary>
    private static string Describe(Exception exception) =>
        MultiplayerFailureText.Describe(exception).ToUpperInvariant();
}
