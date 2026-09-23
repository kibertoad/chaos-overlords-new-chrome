using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Drives one online match: the event stream in, the turn barrier out, and an authoritative state
/// that only ever advances by applying a sealed turn.
/// </summary>
/// <remarks>
/// <para>
/// The session owns the authoritative match and no one else touches it. The interface plans on a
/// copy — see <see cref="MatchStateClone"/> for why — and receives a fresh one through
/// <see cref="TryDequeueNotice"/> each time a turn resolves.
/// </para>
/// <para>
/// Three background tasks, and nothing else. The <em>pump</em> reads the log and acts on every fact,
/// so the order in which turns are applied is the order the log delivered them. The <em>outbox</em>
/// sends this player's order document, and the <em>reporter</em> sends resolved-turn hashes, so a
/// game loop never waits on a round trip. All answer through the notice queue, which the game
/// thread drains, and any one failing for good stops the others: there is no half-alive session
/// that reads turns it can no longer answer.
/// </para>
/// <para>
/// Nothing here ends a match because the network hiccuped. Every call a received fact leads to is
/// idempotent and retried — see <see cref="TransientFailure"/> — and only a refusal that will keep
/// being refused, or a payload that cannot be made sense of, raises
/// <see cref="MultiplayerNotice.Failed"/>.
/// </para>
/// </remarks>
public sealed partial class MultiplayerMatchSession : IAsyncDisposable
{
    private readonly MatchHandle _match;
    private readonly OriginalData _definitions;
    private readonly ConcurrentQueue<MultiplayerNotice> _notices = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationToken _stoppingToken;
    private readonly TimeSpan? _streamIdleTimeout;
    private readonly TimeSpan? _streamOutageBudget;
    private readonly RetryPolicy _streamRetryPolicy;
    private readonly RetryPolicy _callRetryPolicy;
    private readonly Dictionary<string, int> _slotsByPlayerId;
    private readonly Dictionary<string, PendingTakeoverVote> _takeoverVotes = new(StringComparer.Ordinal);

    /// <summary>Seats that have said they are done with <see cref="_readinessTurn"/>.</summary>
    private readonly HashSet<string> _readyPlayerIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether the server is answering, as the stream, the pump's calls and the outbox each see it.
    /// </summary>
    /// <remarks>
    /// Three lanes because three loops retry on their own. One flag shared between them made the
    /// reconnect modal flap: a submission that got through while the stream was still down
    /// announced a recovery, and the stream's next attempt took it back.
    /// </remarks>
    private readonly ConnectionHealth _health;
    private readonly ConnectionHealth.Lane _streamLane;
    private readonly ConnectionHealth.Lane _pumpLane;
    private readonly ConnectionHealth.Lane _outboxLane;
    private readonly Lock _disposalGate = new();

    private MatchReplayRecorder _replay;
    private Task? _pump;
    private Task? _outbox;
    private Task? _reporter;
    private Task? _disposal;
    private int _failed;
    private string? _pumpOperation;
    private string? _outboxOperation;

    /// <summary>
    /// The last event sequence applied to the state. Every event at or below it has been acted on.
    /// </summary>
    private int _resumeAfterSeq;
    private int _readinessTurn = -1;

    /// <summary>
    /// The seats the server is still waiting on before readiness alone seals a turn.
    /// </summary>
    /// <remarks>
    /// Active seats plus temporarily absent seats whose vote still says to wait. Explicit leavers
    /// and approved computer seats are excluded. Kept from the match view, which the pump refreshes whenever the roster
    /// changes, and only ever read for what is on screen — nothing about the turn depends on it.
    /// </remarks>
    private HashSet<int> _awaitedSlots;

    private bool _uploadInitialSnapshot;

    /// <summary>
    /// Whether this client is the host, as the roster last said.
    /// </summary>
    /// <remarks>
    /// Written by the pump and read by the game thread, hence <c>volatile</c>. The server promotes
    /// a successor whenever the host leaves, is kicked or is voted onto computer control, and the
    /// promoted client is the only one that may repair a desync — so a flag frozen at bootstrap
    /// would leave every client waiting on a host that no longer exists.
    /// </remarks>
    private volatile bool _isHost;

    /// <summary>
    /// Cancels the event stream currently being read, so the pump can start a fresh cycle.
    /// </summary>
    /// <remarks>
    /// Written by the pump and read by whoever calls <see cref="RequestResync"/>, which is the game
    /// thread. It is never the session's own stopping source: a resync ends one connection, not the
    /// session.
    /// </remarks>
    private CancellationTokenSource? _streamCycle;

    /// <summary>Set by <see cref="RequestResync"/>; cleared by the pump when it acts on it.</summary>
    private int _resyncRequested;

    private MultiplayerMatchSession(
        MultiplayerSessionOptions options,
        MatchReplayRecorder replay,
        PlayerView self,
        Dictionary<string, int> slotsByPlayerId,
        bool isRestoring)
    {
        _match = options.Match;
        _definitions = options.Definitions;
        _streamIdleTimeout = options.StreamIdleTimeout;
        _streamOutageBudget = options.StreamOutageBudget;
        _streamRetryPolicy = options.StreamRetryPolicy ?? RetryPolicy.Stream;
        _callRetryPolicy = options.CallRetryPolicy ?? RetryPolicy.Call;
        _reportFlushGrace = options.ReportFlushGrace ?? DefaultReportFlushGrace;
        _stoppingToken = _stopping.Token;
        _replay = replay;
        _slotsByPlayerId = slotsByPlayerId;
        _awaitedSlots = [.. slotsByPlayerId.Values];
        _resumeAfterSeq = options.ResumeAfterSeq;
        PlayerId = self.Id;
        Slot = self.Slot;
        _isHost = self.IsHost;
        IsRestoring = isRestoring;
        _uploadInitialSnapshot = IsHost && !isRestoring;
        _health = new ConnectionHealth((connected, detail, attempt) =>
            _notices.Enqueue(new MultiplayerNotice.ConnectionChanged(connected, detail, attempt)));
        _streamLane = _health.Open("stream");
        _pumpLane = _health.Open("pump");
        _outboxLane = _health.Open("outbox");
        _reportLane = _health.Open("report");
        Bootstrap = new MatchBootstrap(
            MatchStateClone.Of(replay.State, options.Definitions),
            ParseInstant(options.View.Turn?.DeadlineAt));
    }

    /// <summary>This client's player id.</summary>
    public string PlayerId { get; }

    /// <summary>The seat this client plays. Every op it records names this slot.</summary>
    public int Slot { get; }

    /// <summary>Whether this client is the one that repairs a desync by uploading a snapshot.</summary>
    public bool IsHost => _isHost;

    /// <summary>Whether startup is reconstructing turns or handovers from durable history.</summary>
    public bool IsRestoring { get; }

    /// <summary>
    /// How far the server's clock is ahead of this machine's.
    /// </summary>
    /// <remarks>
    /// Every deadline the protocol carries is an instant on the SERVER's clock, so a countdown
    /// taken against the local one is wrong by however far the two have drifted — and an
    /// unsynchronised desktop clock is an ordinary thing to have. The server is still the authority
    /// on when a turn ends; this only makes the courtesy countdown honest.
    /// </remarks>
    public TimeSpan ServerTimeOffset => _match.ServerTimeOffset;

    /// <summary>
    /// The match as generated from the seed, for the interface to plan turn 1 on.
    /// </summary>
    /// <remarks>
    /// Immutable. When <see cref="IsRestoring"/>, the state the interface should actually play on
    /// arrives later through <see cref="MultiplayerNotice.Resumed"/>; this is only where a match
    /// begins, and it is never rewritten to be anything else.
    /// </remarks>
    public MatchBootstrap Bootstrap { get; }

    /// <summary>
    /// Bootstraps the match from the server's seed and roster and starts the event pump.
    /// </summary>
    /// <remarks>
    /// The city is generated rather than downloaded: the server holds no game state, and the
    /// generator is deterministic over the seed, the settings and the seating, so every client
    /// arrives at the same city without any of it crossing the wire.
    /// </remarks>
    public static MultiplayerMatchSession Start(MultiplayerSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var view = options.View;
        RequireResumableSession(view.SessionVersion, "match");
        ValidateBootstrapView(view, options.OwnPlayerId);
        var seed = view.Seed
            ?? throw new MultiplayerProtocolException("the match has started without a seed");
        var self = view.Players.FirstOrDefault(player => player.Id == options.OwnPlayerId)
            ?? throw new MultiplayerProtocolException("this client is not on the match roster");
        if (!MatchBootstrapFactory.IsSeated(self))
        {
            throw new MultiplayerProtocolException(
                $"this client was given slot {self.Slot}, which is not a seat at this table");
        }
        var settings = MultiplayerGameSettings.FromWire(view.Settings.GameSettings);
        var state = MatchBootstrapFactory.Create(options.Definitions, seed, settings, view.Players);
        var replay = new MatchReplayRecorder(state);
        CommandPhase.Enter(replay);

        var isRestoring = options.JoinedInProgress
            || view.CurrentTurn > replay.State.Coordinator.Turn
            || view.Players.Any(player =>
                player.Slot >= 0 && player.Status != WirePlayerStatus.Active);
        var session = new MultiplayerMatchSession(
            options, replay, self, SeatedSlots(view.Players), isRestoring);
        session._pump = Task.Run(() => session.RunPumpAsync(session._stoppingToken));
        session._outbox = Task.Run(() => session.RunOutboxAsync(session._stoppingToken));
        session._reporter = Task.Run(() => session.RunReporterAsync(session._stoppingToken));
        return session;
    }

    /// <summary>The next thing the interface should know about, if anything is waiting.</summary>
    public bool TryDequeueNotice(out MultiplayerNotice notice) => _notices.TryDequeue(out notice!);

    /// <summary>
    /// Drops the event stream and rebuilds the session's state from the server's durable history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The answer to "I should have heard something by now and I have not". A stream can be dead
    /// without saying so — a suspended laptop, a NAT entry that expired — and the request that
    /// preceded the silence may well have succeeded on a fresh connection, so the fact the client
    /// is missing is sitting in the log waiting to be read. This is the recovery the pump already
    /// runs for a sequence gap, offered to a caller that noticed the silence from outside.
    /// </para>
    /// <para>
    /// Safe to call at any time and from any thread, and cheap when nothing is wrong: the restore
    /// replays from the last applied sequence, so a session that was in fact up to date re-reads a
    /// view and carries on. It is not a failure and it does not end anything.
    /// </para>
    /// </remarks>
    public void RequestResync()
    {
        if (Interlocked.Exchange(ref _resyncRequested, 1) != 0) return;
        // The cycle may end on its own between the read and the cancel; the flag is what matters
        // and the next cycle will see it.
        CancelUnlessDisposed(Volatile.Read(ref _streamCycle));
    }

    /// <summary>
    /// Cancels a source read from a field its owner clears and disposes when its work ends.
    /// </summary>
    /// <remarks>
    /// The read and the cancel cannot share a lock — <c>Cancel</c> runs registrations inline on the
    /// calling thread — so the owner can finish in between. A source already disposed has nothing
    /// left to cancel, and saying so must not throw on the interface's thread.
    /// </remarks>
    private static void CancelUnlessDisposed(CancellationTokenSource? source)
    {
        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Its work finished and it was disposed after the caller read it.
        }
    }

    /// <summary>
    /// Votes on whether an absent player's seat should become computer-controlled.
    /// </summary>
    /// <remarks>
    /// Bound to the session's own lifetime as well as the caller's token: the interface fires this
    /// and forgets it, and a vote still retrying after the session has been stopped would be a
    /// request against a match this client has already left.
    /// </remarks>
    public async Task VoteOnTakeoverAsync(
        string playerId,
        TakeoverChoice choice,
        CancellationToken cancellationToken = default)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _stoppingToken);
        try
        {
            await CallAsync(
                token => _match.VoteOnTakeoverAsync(
                    playerId,
                    new TakeoverVoteRequest(choice == TakeoverChoice.Computer
                        ? TakeoverVoteRequestDecision.Computer
                        : TakeoverVoteRequestDecision.Wait),
                    token),
                lane: null,
                lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // The session is stopping, or the caller asked to stop. Neither is news.
            throw;
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException or RetryExhaustedException
            or MultiplayerTimeoutException or HttpRequestException or IOException)
        {
            // A vote that did not land leaves the question open, and the player is the only one who
            // can answer it again — so it goes through the notice queue like every other background
            // result rather than into a diagnostics line nobody is reading.
            _notices.Enqueue(new MultiplayerNotice.TakeoverVoteFailed(
                playerId, choice, Describe(exception)));
        }
    }

    /// <summary>Gives up the seat; the match stops waiting on this player from the next turn.</summary>
    public Task LeaveAsync(CancellationToken cancellationToken) =>
        _match.LeaveAsync(cancellationToken);

    /// <summary>
    /// Stops the background tasks, answering a task that completes when they have finished.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="DisposeAsync"/> so a caller on a thread that must not block — a game
    /// loop — can stop a session now and let it wind down behind them. The returned task owns the
    /// disposal of everything the session holds, so it must be observed by somebody, but not
    /// necessarily by the caller. Calling either more than once answers the same wind-down.
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
        // Before the token goes. A queued hash the server never hears is a turn every other player
        // is still held at, and cancelling first threw away exactly the reports a player quitting
        // the moment a turn resolved had just produced. Bounded; see `FlushReportsAsync`.
        await FlushReportsAsync().ConfigureAwait(false);
        await _stopping.CancelAsync().ConfigureAwait(false);
        foreach (var task in new[] { _pump, _outbox, _reporter })
        {
            if (task is null) continue;
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopping is how a session ends; both tasks are meant to be cancelled.
            }
        }
        _stopping.Dispose();
        _outboxSignal.Dispose();
        _reportSignal.Dispose();
    }

    /// <summary>
    /// Ends the session over something it cannot recover from, once.
    /// </summary>
    /// <remarks>
    /// Either loop can be the one that finds out — a revoked token answers the outbox as readily
    /// as the pump — and whichever does, the other is stopped with it. One <see cref="MultiplayerNotice.Failed"/>
    /// reaches the interface: the second loop ends by cancellation, which is not a failure of its own.
    /// </remarks>
    private void Fail(Exception exception, string? operation)
    {
        if (Interlocked.Exchange(ref _failed, 1) != 0) return;
        _notices.Enqueue(new MultiplayerNotice.Failed(
            Describe(exception), exception, operation, Volatile.Read(ref _resumeAfterSeq)));
        _stopping.Cancel();
    }

    private async Task HandleAsync(MatchEvent @event, CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case TurnSealedEvent sealedTurn:
                lock (_outboxGate) _locallyReadyTurns.Remove(sealedTurn.Payload.Turn);
                await ResolveSealedTurnAsync(
                        sealedTurn.Payload.Turn,
                        sealedTurn.Payload.OrderSetHash,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            case TurnConfirmedEvent confirmed:
                // The verdict this client may have been waiting on. Clearing it here is what stops
                // a settled turn's repair announcement, which every reconnect replays, from being
                // treated as an open question again.
                if (_pendingDesync?.Turn == confirmed.Payload.Turn) _pendingDesync = null;
                await CheckpointIfDueAsync(
                        confirmed.Payload.Turn, confirmed.Payload.StateHash, cancellationToken)
                    .ConfigureAwait(false);
                return;
            case TurnDesyncedEvent desynced:
                await HandleDesyncAsync(desynced, cancellationToken).ConfigureAwait(false);
                return;
            case SnapshotAvailableEvent snapshot:
                await AdoptSnapshotAsync(snapshot.Payload, cancellationToken).ConfigureAwait(false);
                return;
            case TurnOpenedEvent opened:
                _notices.Enqueue(new MultiplayerNotice.DeadlineChanged(
                    opened.Payload.Turn, ParseInstant(opened.Payload.DeadlineAt)));
                return;
            case TurnDeadlineExtendedEvent extended:
                _notices.Enqueue(new MultiplayerNotice.DeadlineChanged(
                    extended.Payload.Turn, ParseInstant(extended.Payload.DeadlineAt)));
                return;
            case TurnReadinessEvent readiness:
                // Kept as the seats behind the names: the interface marks the opponents still
                // drafting under their portraits, and a player id means nothing to a portrait.
                NoteReadiness(readiness.Payload.Turn, readiness.Payload.PlayerId, readiness.Payload.Ready);
                return;
            case MatchStatusChangedEvent status:
                HandleStatus(status.Payload.Status);
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                return;
            case LobbyPlayerLeftEvent:
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                return;
            case MatchTakeoverVoteRequestedEvent requested:
                var takeoverVote = BeginTakeoverVote(
                    requested.Payload.PlayerId, requested.Payload.Turn);
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                PublishTakeoverVote(takeoverVote);
                return;
            case MatchTakeoverVoteCastEvent cast:
                PublishTakeoverVote(RecordTakeoverVote(cast));
                return;
            case MatchTakeoverVoteCancelledEvent cancelled:
                _takeoverVotes.Remove(cancelled.Payload.PlayerId);
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                _notices.Enqueue(new MultiplayerNotice.TakeoverVoteClosed(
                    cancelled.Payload.PlayerId, ComputerControl: false));
                return;
            case MatchPlayerTakenOverEvent takenOver:
                _takeoverVotes.Remove(takenOver.Payload.PlayerId);
                TransferPlayerToComputer(takenOver.Payload.PlayerId);
                // This seat is no longer a human the server accepts writes from. Every later report
                // is answered `403 not_active`, which is not transient, so the session failed with a
                // raw HTTP 403 a turn or two after the modal said the seat had been handed over.
                if (string.Equals(takenOver.Payload.PlayerId, PlayerId, StringComparison.Ordinal))
                    _ownSeatIsComputerControlled = true;
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                _notices.Enqueue(new MultiplayerNotice.TakeoverVoteClosed(
                    takenOver.Payload.PlayerId, ComputerControl: true));
                return;
            case MatchPlayerReturnedEvent returned:
                _takeoverVotes.Remove(returned.Payload.PlayerId);
                if (returned.Payload.ReplacedComputer)
                    TransferPlayerToHuman(returned.Payload.PlayerId);
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                _notices.Enqueue(new MultiplayerNotice.TakeoverVoteClosed(
                    returned.Payload.PlayerId, ComputerControl: false));
                return;
            case MatchLatePlayerJoinedEvent joined:
                AddLatePlayer(joined.Payload.PlayerId, joined.Payload.Slot);
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                return;
            case LobbyPlayerJoinedEvent or LobbyHostChangedEvent:
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                return;
            default:
                // A turn this client has applied needs nothing more said about it, and confirmation
                // is a fact the match view already carries.
                return;
        }
    }

    /// <summary>
    /// Fetches a sealed set, checks it against the digest, applies it and reports the result.
    /// </summary>
    /// <remarks>
    /// Delivery is at least once, so a seal for a turn this client has already resolved is dropped
    /// rather than applied twice: applying turn N leaves the coordinator on N+1, which is the test.
    /// It holds for the turn that ends a match too — the coordinator advances even though the phase
    /// stops short of Command — so a repeat of the final seal is dropped rather than reapplied
    /// against a state that can no longer take one.
    /// </remarks>
    private async Task ResolveSealedTurnAsync(
        int turn,
        string announcedOrderSetHash,
        CancellationToken cancellationToken)
    {
        if (turn < _replay.State.Coordinator.Turn) return;
        // A finished match has no turn left to apply. The coordinator stops short of Command when
        // the match ends, so a successor turn that seals on its deadline — which happens when the
        // server cannot finish the match because a seat has not reported — would throw inside
        // SealedTurnApplier and throw the player off the endgame screen into an error modal.
        if (_replay.State.Outcome is not null) return;
        var (stateHash, includedOwnOrders) = await FetchAndApplySealedTurnAsync(
                turn, announcedOrderSetHash, cancellationToken)
            .ConfigureAwait(false);
        // A turn is locally safe to plan as soon as its sealed set has been verified and applied.
        // The hash report is what tells the server whether peers agreed, but waiting for its HTTP
        // response here made every new turn pay an avoidable round trip and held the event pump up
        // behind a retry. The reporter retains the request in turn order and any desync it causes
        // still arrives on this pump and closes the speculative plan immediately.
        //
        // Queued BEFORE the handover, which is neither free nor instant: a turn the player has been
        // told about is one this client is already committed to reporting, so quitting on the very
        // frame it arrived still flushes the hash the rest of the table is waiting for.
        Forget(QueueReportAsync(turn, stateHash));
        var (state, planning) = HandOver();
        _notices.Enqueue(new MultiplayerNotice.TurnResolved(
            turn, state, stateHash, includedOwnOrders, planning));
    }

    /// <summary>
    /// The sealed set for exactly the turn the match is on, verified and applied.
    /// </summary>
    /// <remarks>
    /// Three things have to agree before a set touches the state: the set is for the turn that was
    /// asked for, that turn is the one the match is waiting to resolve, and the digest is the one
    /// the log announced. A set for any other turn applied here would be a desync nobody reported.
    /// </remarks>
    /// <returns>
    /// The hash to report, and whether the set carried a document for this client's own seat — the
    /// one authoritative answer to "did what I sent make it into the turn".
    /// </returns>
    private async Task<(string StateHash, bool IncludedOwnOrders)> FetchAndApplySealedTurnAsync(
        int turn,
        string announcedOrderSetHash,
        CancellationToken cancellationToken)
    {
        var current = _replay.State.Coordinator.Turn;
        if (turn != current)
        {
            throw new MultiplayerProtocolException(
                $"the log sealed turn {turn} while the match was on turn {current}");
        }
        // From the replay's prefetch when one was started for this turn, and from the server
        // otherwise; see `SealedSetAsync`.
        var sealedOrders = await SealedSetAsync(turn, cancellationToken).ConfigureAwait(false);
        RequireSealedSet(sealedOrders, turn, announcedOrderSetHash);
        var includedOwnOrders = sealedOrders.Players.Any(entry => entry.Slot == Slot);
        return (SealedTurnApplier.Apply(_replay, sealedOrders), includedOwnOrders);
    }

    /// <summary>
    /// The two copies the interface needs, both built here rather than on the game thread.
    /// </summary>
    /// <remarks>
    /// The authoritative copy it owns, and the planning copy built over it — which is another
    /// native save and load plus a walk of the coordinator up to the local seat. Doing the second
    /// one on the game thread meant that work landed in the frame the new turn appeared, on top of
    /// a clone the notice had already made of the same state. The restore path has always handed
    /// the finished planning copy over instead; this is that, for every other path.
    /// </remarks>
    private (MatchState State, SpeculativeTurn? Planning) HandOver()
    {
        var state = MatchStateClone.Of(_replay.State, _definitions);
        // A turn that ended the match leaves no turn to plan, and the interface shows the endgame
        // from the state alone. So does a state that stopped anywhere but Command: `SpeculativeTurn`
        // refuses to plan on one, and refusing HERE is an `InvalidOperationException` on the pump,
        // which the catch-all turns into a failed session. The interface has always had a way to
        // stand down from that state gracefully — it says the match reached one this client cannot
        // play on — and it reaches that way by being handed no planning copy, exactly as it is at
        // the end of a match.
        return (state, IsPlannable(state) ? SpeculativeTurn.For(state, _definitions, Slot) : null);
    }

    /// <summary>Whether there is a turn on this state for the local seat to plan.</summary>
    private static bool IsPlannable(MatchState state) =>
        state.Outcome is null && state.Coordinator.Phase == TurnPhase.Command;

    private PendingTakeoverVote BeginTakeoverVote(string playerId, int turn)
    {
        var vote = new PendingTakeoverVote(playerId, turn);
        _takeoverVotes[playerId] = vote;
        return vote;
    }

    private PendingTakeoverVote RecordTakeoverVote(MatchTakeoverVoteCastEvent cast)
    {
        if (!_takeoverVotes.TryGetValue(cast.Payload.PlayerId, out var vote))
            throw new MultiplayerProtocolException("a takeover vote was cast before it was requested");
        vote.Votes[cast.Payload.VoterPlayerId] = cast.Payload.Decision
            == MatchTakeoverVoteCastEventPayloadDecision.Computer
                ? TakeoverChoice.Computer
                : TakeoverChoice.Wait;
        return vote;
    }

    private void PublishTakeoverVote(PendingTakeoverVote vote) =>
        _notices.Enqueue(new MultiplayerNotice.TakeoverVoteChanged(
            vote.PlayerId,
            vote.Turn,
            new Dictionary<string, TakeoverChoice>(vote.Votes, StringComparer.Ordinal)));

    /// <summary>
    /// A snapshot's state, checked to be one this build reads and to hash to what it claims.
    /// </summary>
    /// <remarks>
    /// A newer save format is said plainly rather than discovered as a decoding failure: the repair
    /// was written by a newer build of the game, and no amount of retrying will make these bytes
    /// readable. Base64 and the native reader each have their own way of refusing a malformed
    /// body, and neither is a failure of the connection, so they are named as what they are too.
    /// </remarks>
    private MatchState ReadVerifiedSnapshot(SnapshotView snapshot)
    {
        RequireResumableSession(snapshot.SessionVersion, "snapshot");
        if (snapshot.FormatVersion > NativeSaveSerializer.CurrentFormatVersion)
        {
            throw new MultiplayerProtocolException(
                $"the repair for turn {snapshot.Turn} is save format {snapshot.FormatVersion}, and this build "
                + $"reads up to {NativeSaveSerializer.CurrentFormatVersion}");
        }
        MatchState restored;
        try
        {
            restored = MatchStateClone.FromBase64(snapshot.Body, _definitions);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            throw new MultiplayerProtocolException(
                $"the repair for turn {snapshot.Turn} is not a match this build can read: {exception.Message}",
                exception);
        }
        var stateHash = MatchStateHasher.ComputeFingerprint(restored);
        if (!string.Equals(stateHash, snapshot.StateHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"the snapshot for turn {snapshot.Turn} does not hash to the state it claims");
        }
        return restored;
    }

    /// <summary>
    /// Refuses a session this build cannot play on.
    /// </summary>
    /// <remarks>
    /// The session version is asked, not the protocol version. The protocol was already settled by
    /// the handshake, and it says nothing about the match: a session created by a client that spoke
    /// an older protocol is played here unchanged, as long as the stored session is the shape this
    /// build knows how to carry on. What would break is a session whose rules, orders or state
    /// hashing are not the ones here, and that is exactly what the session version names.
    /// </remarks>
    private static void RequireResumableSession(long actual, string source)
    {
        if (actual == MultiplayerSessionVersion.Current) return;
        throw new MultiplayerProtocolException(
            $"the {source} is session version {actual}, but this build plays "
            + MultiplayerSessionVersion.Current);
    }

    /// <summary>Set once the other players have voted this client's own seat onto the computer.</summary>
    /// <remarks>
    /// Written by the pump — from the takeover event, and from a report the roster moved under —
    /// and by the outbox, which meets the same 403 on a submission, hence <c>volatile</c>. Losing
    /// the race costs one more request that is refused the same way.
    /// </remarks>
    private volatile bool _ownSeatIsComputerControlled;

    /// <summary>
    /// Sends this client's already-queued state-hash report.
    /// </summary>
    /// <remarks>
    /// A turn the server has already confirmed is a success, not a failure. Since reports on settled
    /// turns were refused, the server answers `409 turn_confirmed` for one, and a 409 is below 500
    /// so the retry policy treats it as terminal — which ended the session of any client whose
    /// redundant report lost a race, and of any client whose report completed the consensus but
    /// whose response was lost and therefore retried. The turn is confirmed either way, which is
    /// what this report was asking for.
    /// </remarks>
    private async Task SendReportAsync(PendingReport report, CancellationToken cancellationToken)
    {
        // A seat the server no longer counts as human has nothing to report. Carrying on and being
        // refused is how this used to surface, one 403 at a time.
        if (_ownSeatIsComputerControlled) return;
        try
        {
            await CallAsync(
                token => _match.ReportAsync(
                    report.Turn,
                    report.Request,
                    token),
                _reportLane,
                cancellationToken).ConfigureAwait(false);
        }
        catch (MultiplayerApiException exception) when (exception.Reason == "turn_confirmed")
        {
            // Nothing to do and nothing wrong: the verdict this report was asking for is in.
        }
        catch (MultiplayerApiException exception) when (exception.Reason == "not_active")
        {
            // The roster moved under this report: the seat was handed to the computer, or removed,
            // between the seal and now. Stop reporting rather than failing the session on a 403.
            _ownSeatIsComputerControlled = true;
        }
    }

    private async Task PublishMatchAsync(CancellationToken cancellationToken)
    {
        var view = await ReadMatchViewAsync(cancellationToken).ConfigureAwait(false);
        // A seat that has gone quiet is also no longer one the turn is waiting on, so drop any
        // readiness it had left behind rather than counting it towards a total it is not part of.
        _readyPlayerIds.RemoveWhere(
            ready => !view.Players.Any(player => player.Id == ready && IsAwaitedHuman(player)));
        // What is on screen changes when a seat is vacated, not only when somebody toggles
        // readiness, so it is said here too — otherwise "READY 2/4" keeps a seat count that is no
        // longer true, and a vacated seat keeps its WAIT, until the next player happens to toggle.
        PublishReadiness();
        _notices.Enqueue(new MultiplayerNotice.MatchUpdated(view));
    }

    /// <summary>The match as the server now describes it, with the roster facts taken from it.</summary>
    private async Task<MatchView> ReadMatchViewAsync(CancellationToken cancellationToken)
    {
        var detail = await CallAsync(
            token => _match.GetAsync(token), _pumpLane, cancellationToken).ConfigureAwait(false);
        var view = detail.Match;
        RequireResumableSession(view.SessionVersion, "match");
        // Follow the roster's word on who hosts; the promoted client repairs desyncs.
        _isHost = string.Equals(view.HostPlayerId, PlayerId, StringComparison.Ordinal);
        _awaitedSlots = view.Players
            .Where(player => MatchBootstrapFactory.IsSeated(player) && IsAwaitedHuman(player))
            .Select(player => player.Slot)
            .ToHashSet();
        return view;
    }

    /// <summary>
    /// Says which of the awaited seats have finished the turn being planned.
    /// </summary>
    /// <remarks>
    /// Readiness belongs to one turn: a roster kept for an earlier one says nothing about this one,
    /// so it reports nobody ready rather than carrying the old one forward.
    /// </remarks>
    private void PublishReadiness()
    {
        var turn = _replay.State.Coordinator.Turn;
        _notices.Enqueue(new MultiplayerNotice.ReadinessChanged(
            turn, _readinessTurn == turn ? ReadySlots() : [], AwaitedSlots()));
    }

    /// <summary>The seats of the players that have said they are done with the open turn.</summary>
    private HashSet<int> ReadySlots()
    {
        var slots = new HashSet<int>();
        foreach (var playerId in _readyPlayerIds)
        {
            if (_slotsByPlayerId.TryGetValue(playerId, out var slot)) slots.Add(slot);
        }
        return slots;
    }

    /// <summary>
    /// A copy of the awaited seats, because a notice outlives the roster it was made from.
    /// </summary>
    /// <remarks>
    /// The pump replaces the set whenever the roster changes, and the game thread reads notices
    /// whenever it next draws; handing out the live set would let a frame see a roster from after
    /// the tally it is drawn beside.
    /// </remarks>
    private HashSet<int> AwaitedSlots() => [.. _awaitedSlots];

    /// <summary>
    /// One protocol call, retried while the failure is only this attempt's.
    /// </summary>
    /// <remarks>
    /// Every call routed through here is idempotent: the reads plainly so, and the two writes by
    /// definition — a report restates a hash the server already holds, and an order document replaces
    /// what was held rather than adding to it. That is what makes retrying safe, and retrying is what
    /// keeps a restarted server from ending a match that was otherwise going fine.
    /// </remarks>
    /// <param name="lane">
    /// Whose health the attempts report into, or null for a one-off the caller answers for itself.
    /// </param>
    private async Task<T> CallAsync<T>(
        Func<CancellationToken, Task<T>> call,
        ConnectionHealth.Lane? lane,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        if (ReferenceEquals(lane, _pumpLane)) Volatile.Write(ref _pumpOperation, operation);
        if (ReferenceEquals(lane, _outboxLane)) Volatile.Write(ref _outboxOperation, operation);
        try
        {
            var result = await TransientFailure.CallAsync(
                call,
                _callRetryPolicy,
                onRetry: lane is null
                    ? null
                    : (exception, attempt) => lane.Failed(Describe(exception), attempt),
                cancellationToken).ConfigureAwait(false);
            lane?.Recovered();
            return result;
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException)
        {
            // A refusal, or an answer this build cannot read, still proves the server answered.
            // The caller decides what it means for this operation, while the connection lane can
            // stop reporting an outage.
            lane?.Recovered();
            throw;
        }
    }

    /// <summary>
    /// Keeps the roster of seats that have said they are done with the open turn.
    /// </summary>
    /// <remarks>
    /// A turn's readiness is forgotten when a later turn's arrives, so it never carries over. Only
    /// seats held by a human player are kept, which is the roster the server waits on.
    /// </remarks>
    private void NoteReadiness(int turn, string playerId, bool ready)
    {
        if (!_slotsByPlayerId.ContainsKey(playerId)) return;
        if (turn != _readinessTurn)
        {
            _readinessTurn = turn;
            _readyPlayerIds.Clear();
        }
        if (ready) _readyPlayerIds.Add(playerId);
        else _readyPlayerIds.Remove(playerId);
        // For the turn the event names, not the one this client has replayed to: the server can be
        // a turn ahead of a client that is still applying the seal before it.
        _notices.Enqueue(
            new MultiplayerNotice.ReadinessChanged(turn, ReadySlots(), AwaitedSlots()));
    }

    private void HandleStatus(MatchStatus status)
    {
        switch (status)
        {
            case MatchStatus.Finished:
                _notices.Enqueue(new MultiplayerNotice.MatchFinished());
                return;
            case MatchStatus.Abandoned:
                _notices.Enqueue(new MultiplayerNotice.MatchAbandoned());
                return;
            default:
                // Lobby, running and desynced are states the match view already describes, and the
                // desync pause has an event of its own that carries the hashes with it.
                return;
        }
    }

    /// <summary>Every seat taken when the match started, by the player that took it.</summary>
    private static Dictionary<string, int> SeatedSlots(IReadOnlyList<PlayerView> players)
    {
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var player in players)
        {
            if (MatchBootstrapFactory.IsSeated(player)) slots[player.Id] = player.Slot;
        }
        return slots;
    }

    /// <summary>
    /// Whether the server has finished starting a match, so a session can be built from the view.
    /// </summary>
    /// <remarks>
    /// Starting is several writes at the server: the match turns <c>running</c> on turn 0 first, and
    /// only then seats the roster and opens turn 1. A lobby poll that lands between them reads a
    /// running match that is not playable yet. That view is not a broken match, only an early one,
    /// and the next poll brings the finished one.
    /// </remarks>
    public static bool HasFinishedStarting(MatchView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Status is MatchStatus.Running or MatchStatus.Desynced
            && view.CurrentTurn >= 1
            // Only the lobby's slot -1 means "not seated yet". A slot past the board is a finished
            // start this client cannot play, which the bootstrap refuses with a reason; counting it
            // as unfinished would leave the player waiting on a start that has already happened.
            && !view.Players.Any(player => player.Status == WirePlayerStatus.Active && player.Slot < 0);
    }

    /// <summary>
    /// Refuses a running match whose initial authoritative view contradicts itself before a city is
    /// generated or the interface is allowed to issue an order against it.
    /// </summary>
    /// <remarks>
    /// This is intentionally a local preflight over an existing <see cref="MatchView"/> rather than
    /// another start request. The server's compare-and-swap remains the authority that prevents two
    /// starts; this catches an incomplete response, a stale handover, or a server-side transition
    /// that was observed halfway through on the client side.
    /// </remarks>
    private static void ValidateBootstrapView(MatchView view, string ownPlayerId)
    {
        if (view.Status is not (MatchStatus.Running or MatchStatus.Desynced))
            throw new MultiplayerProtocolException($"a match in state {view.Status} cannot be started");
        if (view.CurrentTurn < 1)
            throw new MultiplayerProtocolException(
                $"a started match cannot be on turn {view.CurrentTurn}");
        if (view.Turn is not { } turn || turn.Number != view.CurrentTurn)
            throw new MultiplayerProtocolException(
                "the match's open turn does not agree with its current turn");
        if (view.Status == MatchStatus.Running && turn.Status != TurnStatus.Open)
            throw new MultiplayerProtocolException(
                $"a running match has a {turn.Status} current turn instead of an open one");

        var seated = view.Players.Where(MatchBootstrapFactory.IsSeated).ToArray();
        if (seated.Length == 0
            || seated.Select(player => player.Id).Distinct(StringComparer.Ordinal).Count() != seated.Length
            || seated.Select(player => player.Slot).Distinct().Count() != seated.Length)
        {
            throw new MultiplayerProtocolException("the match roster has duplicate or missing seats");
        }
        var self = seated.SingleOrDefault(player => player.Id == ownPlayerId);
        if (self is null || self.Status is not (WirePlayerStatus.Active or WirePlayerStatus.TakeoverPending))
            throw new MultiplayerProtocolException("this client does not hold an active roster seat in the match");
    }

    private static bool IsAwaitedHuman(PlayerView player) =>
        player.Status is WirePlayerStatus.Active or WirePlayerStatus.TakeoverPending;

    /// <summary>
    /// An ISO instant, or null when there is none to read.
    /// </summary>
    /// <remarks>
    /// A timestamp this client cannot parse is treated as no deadline rather than as a failure. The
    /// countdown is a courtesy — the server decides when a turn seals whatever the clock on screen
    /// says — so losing it is not worth ending a match over.
    /// </remarks>
    private static DateTimeOffset? ParseInstant(string? value) =>
        value is not null
        && DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant)
            ? instant
            : null;

    private static string Describe(Exception exception) => MultiplayerFailureText.Describe(exception);

    private sealed record PendingTakeoverVote(string PlayerId, int Turn)
    {
        internal Dictionary<string, TakeoverChoice> Votes { get; } = new(StringComparer.Ordinal);
    }
}
