using System.Collections.Concurrent;
using System.Globalization;
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
/// Two background tasks, and nothing else. The <em>pump</em> reads the log and acts on every fact,
/// so the order in which turns are applied is the order the log delivered them. The <em>outbox</em>
/// sends this player's order document, so a game loop never waits on a round trip. Both answer
/// through the notice queue, which the game thread drains.
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
    private const int EventHistoryPageSize = 200;

    private readonly MatchHandle _match;
    private readonly OriginalData _definitions;
    private readonly ConcurrentQueue<MultiplayerNotice> _notices = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<string, int> _slotsByPlayerId;
    private readonly Dictionary<string, PendingTakeoverVote> _takeoverVotes = new(StringComparer.Ordinal);

    /// <summary>Seats that have said they are done with <see cref="_readinessTurn"/>.</summary>
    private readonly HashSet<string> _readyPlayerIds = new(StringComparer.Ordinal);

    private MatchReplayRecorder _replay;
    private Task? _pump;
    private Task? _outbox;
    private int _resumeAfterSeq;
    private int _readinessTurn = -1;

    /// <summary>
    /// How many seats the server is still waiting on before readiness alone seals a turn.
    /// </summary>
    /// <remarks>
    /// Active seats plus temporarily absent seats whose vote still says to wait. Explicit leavers
    /// and approved computer seats are excluded. Kept from the match view, which the pump refreshes whenever the roster
    /// changes, and only ever read for the line on screen — nothing about the turn depends on it.
    /// </remarks>
    private int _awaitedSeats;

    /// <summary>
    /// 1 while the server is answering, 0 while it is not.
    /// </summary>
    /// <remarks>
    /// An <c>int</c> through <see cref="Interlocked"/> because both background tasks report into it:
    /// the pump when the stream drops, and the outbox when a submission needs another attempt.
    /// </remarks>
    private int _connected = 1;
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

    private MultiplayerMatchSession(
        MultiplayerSessionOptions options,
        MatchReplayRecorder replay,
        PlayerView self,
        Dictionary<string, int> slotsByPlayerId,
        bool isRestoring)
    {
        _match = options.Match;
        _definitions = options.Definitions;
        _replay = replay;
        _slotsByPlayerId = slotsByPlayerId;
        _awaitedSeats = slotsByPlayerId.Count;
        _resumeAfterSeq = options.ResumeAfterSeq;
        PlayerId = self.Id;
        Slot = self.Slot;
        _isHost = self.IsHost;
        IsRestoring = isRestoring;
        _uploadInitialSnapshot = IsHost && !isRestoring;
    }

    /// <summary>This client's player id.</summary>
    public string PlayerId { get; }

    /// <summary>The seat this client plays. Every op it records names this slot.</summary>
    public int Slot { get; }

    /// <summary>Whether this client is the one that repairs a desync by uploading a snapshot.</summary>
    public bool IsHost => _isHost;

    /// <summary>Whether startup is reconstructing turns or handovers from durable history.</summary>
    public bool IsRestoring { get; }

    /// <summary>The first Command phase, for the interface to plan turn 1 on.</summary>
    public MatchState InitialState { get; private set; } = null!;

    /// <summary>
    /// When the open turn seals regardless of readiness, as the match view last said.
    /// </summary>
    /// <remarks>
    /// Taken from the view at bootstrap rather than waited for on the stream. The event that opened
    /// turn 1 was published before this client started reading, so resuming from the view's sequence
    /// number skips it by design — and without this the first turn of every match would run with no
    /// countdown on screen.
    /// </remarks>
    public DateTimeOffset? InitialDeadline { get; private set; }

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
        var seed = view.Seed
            ?? throw new MultiplayerProtocolException("the match has started without a seed");
        var self = view.Players.FirstOrDefault(player => player.Id == options.OwnPlayerId)
            ?? throw new MultiplayerProtocolException("this client is not on the match roster");
        if (self.Slot is < 0 or >= MatchLimits.PlayerCount)
        {
            throw new MultiplayerProtocolException(
                $"this client was given slot {self.Slot}, which is not a seat at this table");
        }
        var settings = MultiplayerGameSettings.FromWire(view.Settings.GameSettings);
        if (view.CurrentTurn < 1)
        {
            throw new MultiplayerProtocolException(
                $"a started match cannot be on turn {view.CurrentTurn}");
        }
        var state = MatchBootstrapFactory.Create(options.Definitions, seed, settings, view.Players);
        var replay = new MatchReplayRecorder(state);
        CommandPhase.Enter(replay);

        var isRestoring = options.JoinedInProgress
            || view.CurrentTurn > replay.State.Coordinator.Turn
            || view.Players.Any(player =>
                player.Slot >= 0 && player.Status != WirePlayerStatus.Active);
        var session = new MultiplayerMatchSession(
            options, replay, self, SeatedSlots(view.Players), isRestoring);
        session.InitialState = MatchStateClone.Of(state, options.Definitions);
        session.InitialDeadline = ParseInstant(view.Turn?.DeadlineAt);
        session._pump = Task.Run(() => isRestoring
            ? session.RestoreAndPumpAsync(session._stopping.Token)
            : session.PumpAsync(session._stopping.Token));
        session._outbox = Task.Run(() => session.DrainOutboxAsync(session._stopping.Token));
        return session;
    }

    /// <summary>The next thing the interface should know about, if anything is waiting.</summary>
    public bool TryDequeueNotice(out MultiplayerNotice notice) => _notices.TryDequeue(out notice!);

    /// <summary>Votes on whether an absent player's seat should become computer-controlled.</summary>
    public Task VoteOnTakeoverAsync(
        string playerId,
        TakeoverChoice choice,
        CancellationToken cancellationToken = default) =>
        CallAsync(
            token => _match.VoteOnTakeoverAsync(
                playerId,
                new TakeoverVoteRequest(choice == TakeoverChoice.Computer
                    ? TakeoverVoteRequestDecision.Computer
                    : TakeoverVoteRequestDecision.Wait),
                token),
            cancellationToken);

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
    /// necessarily by the caller.
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
        foreach (var task in new[] { _pump, _outbox })
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
    }

    /// <summary>
    /// Reads the log forever, acting on every fact.
    /// </summary>
    /// <remarks>
    /// A failure that ends the stream ends the session with it: a revoked token means the player was kicked
    /// or was kicked, and there is nothing left to read. Everything that describes one attempt is
    /// retried — the stream by itself, and the calls a fact leads to by <see cref="CallAsync"/>.
    /// </remarks>
    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        var stream = new MatchEventStream(
            _match,
            RetryPolicy.Stream,
            onReconnect: (exception, _) => Report(connected: false, Describe(exception)),
            onConnected: () => Report(connected: true, detail: null));
        try
        {
            if (_uploadInitialSnapshot)
            {
                await UploadInitialSnapshotAsync(cancellationToken).ConfigureAwait(false);
                _uploadInitialSnapshot = false;
            }
            await foreach (var @event in stream
                .ReadAsync(_resumeAfterSeq, cancellationToken).ConfigureAwait(false))
            {
                await HandleAsync(@event, cancellationToken).ConfigureAwait(false);
                _resumeAfterSeq = Math.Max(_resumeAfterSeq, @event.Seq);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ordinary shutdown.
        }
        catch (Exception exception)
        {
            _notices.Enqueue(new MultiplayerNotice.Failed(Describe(exception), exception));
        }
    }

    /// <summary>
    /// Reconciles a newly created client session with a match that has already advanced, then starts
    /// the ordinary gapless event pump from the match view used for that reconstruction.
    /// </summary>
    private async Task RestoreAndPumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var detail = await CallAsync(
                token => _match.GetAsync(token), cancellationToken).ConfigureAwait(false);
            var view = detail.Match;
            if (view.Status == MatchStatus.Abandoned)
            {
                _notices.Enqueue(new MultiplayerNotice.MatchAbandoned());
                return;
            }
            AdoptHost(view);
            _awaitedSeats = view.Players.Count(
                player => player.Slot >= 0 && IsAwaitedHuman(player));
            InitialDeadline = ParseInstant(view.Turn?.DeadlineAt);

            if (view.CurrentTurn < 1)
            {
                throw new MultiplayerProtocolException(
                    $"a started match cannot be on turn {view.CurrentTurn}");
            }

            // Turn 1 included. The host uploads a bootstrap snapshot for turn 0, and that row is the
            // recovery baseline until a desync requires a newer one — so a client that skipped it on
            // turn 1 either refused the only snapshot there was, or bootstrapped a city of its own
            // next to everyone else's.
            var snapshot = await LatestSnapshotOrNullAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is not null) AdoptResumeSnapshot(snapshot, view.CurrentTurn);

            await ReplayEventHistoryAsync(view.LastEventSeq, cancellationToken)
                .ConfigureAwait(false);

            if (_replay.State.Outcome is null
                && (_replay.State.Coordinator.Phase != TurnPhase.Command
                    || _replay.State.Coordinator.Turn != view.CurrentTurn))
            {
                throw new MultiplayerProtocolException(
                    $"the resumed state reached {_replay.State.Coordinator.Phase} turn "
                    + $"{_replay.State.Coordinator.Turn}, but the server is on turn {view.CurrentTurn}");
            }
            if (_replay.State.Outcome is not null
                && view.Status is not (MatchStatus.Finished or MatchStatus.Abandoned))
            {
                throw new MultiplayerProtocolException(
                    "the reconstructed match has ended while the server still reports it in progress");
            }

            var submission = _replay.State.Outcome is null
                ? await CallAsync(
                    token => _match.OwnSubmissionAsync(view.CurrentTurn, token),
                    cancellationToken).ConfigureAwait(false)
                : new OwnSubmissionView(view.CurrentTurn, null, Ready: false, OrdersHash: null);
            ValidateResumeSubmission(submission, view.CurrentTurn);
            InitialState = MatchStateClone.Of(_replay.State, _definitions);
            _resumeAfterSeq = view.LastEventSeq;
            _notices.Enqueue(new MultiplayerNotice.Resumed(
                view,
                MatchStateClone.Of(_replay.State, _definitions),
                submission));
            foreach (var vote in _takeoverVotes.Values.OrderBy(item => item.PlayerId, StringComparer.Ordinal))
                PublishTakeoverVote(vote);

            await PumpAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ordinary shutdown while the historical turns were being reconstructed.
        }
        catch (Exception exception)
        {
            _notices.Enqueue(new MultiplayerNotice.Failed(Describe(exception), exception));
        }
    }

    /// <summary>
    /// Reconstructs controller handovers and sealed turns in their authoritative log order.
    /// </summary>
    /// <remarks>
    /// Vote requests and choices never change the deterministic model. Replaying only the approved
    /// takeover fact preserves the exact boundary between a human-held idle turn and the first turn
    /// on which every client may computer-plan that seat.
    /// </remarks>
    private async Task ReplayEventHistoryAsync(int throughSeq, CancellationToken cancellationToken)
    {
        var after = 0;
        while (after < throughSeq)
        {
            var page = await CallAsync(
                token => _match.EventsAsync(after, EventHistoryPageSize, token),
                cancellationToken).ConfigureAwait(false);
            if (page.Events.Count == 0)
            {
                throw new MultiplayerProtocolException(
                    $"the event history ended at sequence {after}, before sequence {throughSeq}");
            }

            foreach (var @event in page.Events)
            {
                var expected = after + 1;
                if (@event.Seq != expected)
                {
                    throw new MultiplayerProtocolException(
                        $"the event history jumped from sequence {after} to {@event.Seq}");
                }
                if (!string.Equals(@event.MatchId, _match.MatchId, StringComparison.Ordinal))
                {
                    throw new MultiplayerProtocolException(
                        $"event sequence {@event.Seq} belongs to another match");
                }
                if (@event.Seq > throughSeq) return;

                await ApplyHistoricalEventAsync(@event, cancellationToken).ConfigureAwait(false);
                after = @event.Seq;
                if (after == throughSeq) return;
            }
        }
    }

    private async Task ApplyHistoricalEventAsync(
        MatchEvent @event,
        CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case MatchTakeoverVoteRequestedEvent requested:
                BeginTakeoverVote(requested.Payload.PlayerId, requested.Payload.Turn);
                return;
            case MatchTakeoverVoteCastEvent cast:
                RecordTakeoverVote(cast);
                return;
            case MatchTakeoverVoteCancelledEvent cancelled:
                _takeoverVotes.Remove(cancelled.Payload.PlayerId);
                return;
            case MatchPlayerTakenOverEvent takenOver:
                _takeoverVotes.Remove(takenOver.Payload.PlayerId);
                TransferPlayerToComputer(takenOver.Payload.PlayerId);
                return;
            case MatchPlayerReturnedEvent returned:
                _takeoverVotes.Remove(returned.Payload.PlayerId);
                if (returned.Payload.ReplacedComputer)
                    TransferPlayerToHuman(returned.Payload.PlayerId);
                return;
            case MatchLatePlayerJoinedEvent joined:
                AddLatePlayer(joined.Payload.PlayerId, joined.Payload.Slot);
                return;
            case TurnSealedEvent sealedTurn:
                if (sealedTurn.Payload.Turn < _replay.State.Coordinator.Turn) return;
                if (sealedTurn.Payload.Turn > _replay.State.Coordinator.Turn)
                {
                    throw new MultiplayerProtocolException(
                        $"the event history sealed turn {sealedTurn.Payload.Turn} while the "
                        + $"reconstructed match was still on turn {_replay.State.Coordinator.Turn}");
                }
                await FetchAndApplySealedTurnAsync(
                    sealedTurn.Payload.Turn,
                    sealedTurn.Payload.OrderSetHash,
                    cancellationToken).ConfigureAwait(false);
                return;
        }
    }

    private void ValidateResumeSubmission(OwnSubmissionView submission, int currentTurn)
    {
        if (submission.Turn != currentTurn)
        {
            throw new MultiplayerProtocolException(
                $"the saved draft is for turn {submission.Turn}, but the server is on turn "
                + currentTurn);
        }
        if (submission.Orders is null)
        {
            if (submission.Ready || submission.OrdersHash is not null)
            {
                throw new MultiplayerProtocolException(
                    "the saved draft has readiness or a digest but no order document");
            }
            return;
        }
        var digest = OrderDigest.OfDocument(submission.Orders);
        if (!string.Equals(digest, submission.OrdersHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                "the saved draft does not match the digest the server returned");
        }
        _ = SpeculativeTurn.Restore(
            _replay.State, _definitions, Slot, submission.Orders);
    }

    private async Task<SnapshotView?> LatestSnapshotOrNullAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await CallAsync(
                token => _match.LatestSnapshotAsync(token), cancellationToken).ConfigureAwait(false);
        }
        catch (MultiplayerApiException exception) when (exception.Reason == "no_snapshot")
        {
            return null;
        }
    }

    /// <remarks>
    /// Turn 0 is the host's bootstrap snapshot, taken before a single turn was played. It restores
    /// like any other — the state it holds is the one every client generates for turn 1 — and it is
    /// the baseline every ordinary reconnect replays the server's sealed order sets from.
    /// </remarks>
    private void AdoptResumeSnapshot(SnapshotView snapshot, int currentTurn)
    {
        if (snapshot.Turn < 0 || snapshot.Turn >= currentTurn)
        {
            throw new MultiplayerProtocolException(
                $"the latest snapshot is for turn {snapshot.Turn}, but the server is on turn "
                + currentTurn);
        }
        if (snapshot.FormatVersion > NativeSaveSerializer.CurrentFormatVersion)
        {
            throw new MultiplayerProtocolException(
                $"the repair for turn {snapshot.Turn} is save format {snapshot.FormatVersion}, "
                + $"and this build reads up to {NativeSaveSerializer.CurrentFormatVersion}");
        }
        var restored = ReadSnapshot(snapshot.Body, snapshot.Turn);
        var stateHash = MatchStateHasher.ComputeSha256(restored);
        if (!string.Equals(stateHash, snapshot.StateHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"the snapshot for turn {snapshot.Turn} does not hash to the state it claims");
        }
        if (restored.Outcome is null
            && (restored.Coordinator.Phase != TurnPhase.Command
                || restored.Coordinator.Turn != snapshot.Turn + 1))
        {
            throw new MultiplayerProtocolException(
                $"the snapshot for turn {snapshot.Turn} resumes at "
                + $"{restored.Coordinator.Phase} turn {restored.Coordinator.Turn}");
        }
        _replay = new MatchReplayRecorder(restored);
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
            case TurnConfirmedEvent:
                return;
            case TurnDesyncedEvent desynced:
                await HandleDesyncAsync(desynced, cancellationToken).ConfigureAwait(false);
                return;
            case SnapshotAvailableEvent snapshot:
                await AdoptSnapshotAsync(snapshot.Payload.Turn, cancellationToken)
                    .ConfigureAwait(false);
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
                // Whose readiness it is does not matter to the interface, only how many seats are
                // still being waited on — so the tally is kept here rather than a roster of names.
                NoteReadiness(readiness.Payload.Turn, readiness.Payload.PlayerId, readiness.Payload.Ready);
                return;
            case MatchStatusChangedEvent status:
                HandleStatus(status.Payload.Status);
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                return;
            case LobbyPlayerLeftEvent left:
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
        var stateHash = await FetchAndApplySealedTurnAsync(
                turn, announcedOrderSetHash, cancellationToken)
            .ConfigureAwait(false);
        await ReportAsync(turn, stateHash, cancellationToken).ConfigureAwait(false);
        _notices.Enqueue(new MultiplayerNotice.TurnResolved(
            turn, MatchStateClone.Of(_replay.State, _definitions), stateHash));
    }

    private async Task<string> FetchAndApplySealedTurnAsync(
        int turn,
        string announcedOrderSetHash,
        CancellationToken cancellationToken)
    {
        var sealedOrders = await CallAsync(
            token => _match.SealedOrdersAsync(turn, token), cancellationToken).ConfigureAwait(false);
        if (!string.Equals(
                sealedOrders.OrderSetHash,
                announcedOrderSetHash,
                StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"the sealed-set digest for turn {turn} does not match the event log");
        }
        if (!OrderDigest.Verifies(sealedOrders, sealedOrders.OrderSetHash))
        {
            throw new MultiplayerProtocolException(
                $"the sealed set for turn {turn} does not match the digest the server announced");
        }
        return SealedTurnApplier.Apply(_replay, sealedOrders);
    }

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
    /// The match paused because clients disagreed about a turn.
    /// </summary>
    /// <remarks>
    /// Only the host can repair it, and only with a hash the players themselves already reported in
    /// the greatest number — otherwise a host could desync deliberately and upload a doctored state
    /// as the new truth. A host whose own client is the odd one out therefore has nothing it is
    /// allowed to upload, and says so rather than uploading something the server would refuse.
    /// </remarks>
    private async Task HandleDesyncAsync(TurnDesyncedEvent desynced, CancellationToken cancellationToken)
    {
        var turn = desynced.Payload.Turn;
        var ours = MatchStateHasher.ComputeSha256(_replay.State);
        var canRepair = IsHost && desynced.Payload.CandidateStateHashes.Contains(ours, StringComparer.Ordinal);
        _notices.Enqueue(new MultiplayerNotice.Desynced(turn, canRepair));
        if (!canRepair) return;
        await CallAsync(
            token => _match.UploadSnapshotAsync(
                new UploadSnapshotRequest(
                    turn,
                    // The body is a native save, so the version that describes it is the native
                    // save format's — not the replay format's, which says nothing about these bytes.
                    NativeSaveSerializer.CurrentFormatVersion,
                    ours,
                    MatchStateClone.ToBase64(_replay.State),
                    SummarizeSeats(_replay.State)),
                token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adopts a repaired state and re-reports the turn it settles.
    /// </summary>
    /// <remarks>
    /// The host is already on the state it uploaded, so it has nothing to load; every other client
    /// replaces its own with the snapshot, recomputes the hash and reports again. Loading also
    /// starts a fresh recorder: the old one is bound to the state it was constructed over and
    /// refuses to record another.
    /// </remarks>
    private async Task AdoptSnapshotAsync(int turn, CancellationToken cancellationToken)
    {
        var snapshot = await CallAsync(
            token => _match.SnapshotAsync(turn, token), cancellationToken).ConfigureAwait(false);
        if (snapshot.FormatVersion > NativeSaveSerializer.CurrentFormatVersion)
        {
            // Said plainly rather than discovered as a decoding failure: the repair was written by a
            // newer build of the game, and no amount of retrying will make these bytes readable.
            throw new MultiplayerProtocolException(
                $"the repair for turn {turn} is save format {snapshot.FormatVersion}, and this build "
                + $"reads up to {NativeSaveSerializer.CurrentFormatVersion}");
        }
        var restored = ReadSnapshot(snapshot.Body, turn);
        var stateHash = MatchStateHasher.ComputeSha256(restored);
        if (!string.Equals(stateHash, snapshot.StateHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"the snapshot for turn {turn} does not hash to the state it claims");
        }
        _replay = new MatchReplayRecorder(restored);
        await ReportAsync(turn, stateHash, cancellationToken).ConfigureAwait(false);
        _notices.Enqueue(new MultiplayerNotice.Resynced(
            turn, MatchStateClone.Of(restored, _definitions), stateHash));
    }

    /// <summary>
    /// A snapshot's bytes, with a body this client cannot read named as such.
    /// </summary>
    /// <remarks>
    /// Base64 and the native reader each have their own way of refusing a malformed body, and
    /// neither is a failure of the connection. Left unhandled they would end the match as though it
    /// had been disconnected, which tells a player nothing about what actually went wrong.
    /// </remarks>
    private MatchState ReadSnapshot(string body, int turn)
    {
        try
        {
            return MatchStateClone.FromBase64(body, _definitions);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            throw new MultiplayerProtocolException(
                $"the repair for turn {turn} is not a match this build can read: {exception.Message}",
                exception);
        }
    }

    private Task ReportAsync(int turn, string stateHash, CancellationToken cancellationToken) =>
        CallAsync(
            token => _match.ReportAsync(
                turn,
                new TurnReportRequest(
                    stateHash,
                    _replay.State.Outcome is not null,
                    IsHost ? SummarizeSeats(_replay.State) : null),
                token),
            cancellationToken);

    private async Task PublishMatchAsync(CancellationToken cancellationToken)
    {
        var detail = await CallAsync(
            token => _match.GetAsync(token), cancellationToken).ConfigureAwait(false);
        AdoptHost(detail.Match);
        _awaitedSeats = detail.Match.Players.Count(
            player => player.Slot >= 0 && IsAwaitedHuman(player));
        // A seat that has gone quiet is also no longer one the turn is waiting on, so drop any
        // readiness it had left behind rather than counting it towards a total it is not part of.
        _readyPlayerIds.RemoveWhere(
            ready => !detail.Match.Players.Any(
                player => player.Id == ready && IsAwaitedHuman(player)));
        // The tally on screen changes when a seat is vacated, not only when somebody toggles
        // readiness, so it is said here too — otherwise "READY 2/4" keeps a seat count that is no
        // longer true until the next player happens to toggle.
        PublishReadiness();
        _notices.Enqueue(new MultiplayerNotice.MatchUpdated(detail.Match));
    }

    /// <summary>Follows the roster's word on who hosts; the promoted client repairs desyncs.</summary>
    private void AdoptHost(MatchView view) =>
        _isHost = string.Equals(view.HostPlayerId, PlayerId, StringComparison.Ordinal);

    /// <summary>
    /// Says how many of the awaited seats have finished the turn being planned.
    /// </summary>
    /// <remarks>
    /// Readiness belongs to one turn: a tally kept for an earlier one says nothing about this one,
    /// so it counts as nobody ready rather than carrying the old count forward.
    /// </remarks>
    private void PublishReadiness()
    {
        var turn = _replay.State.Coordinator.Turn;
        _notices.Enqueue(new MultiplayerNotice.ReadinessChanged(
            turn, _readinessTurn == turn ? _readyPlayerIds.Count : 0, _awaitedSeats));
    }

    /// <summary>
    /// One protocol call, retried while the failure is only this attempt's.
    /// </summary>
    /// <remarks>
    /// Every call routed through here is idempotent: the reads plainly so, and the two writes by
    /// definition — a report restates a hash the server already holds, and an order document replaces
    /// what was held rather than adding to it. That is what makes retrying safe, and retrying is what
    /// keeps a restarted server from ending a match that was otherwise going fine.
    /// </remarks>
    private Task<T> CallAsync<T>(
        Func<CancellationToken, Task<T>> call,
        CancellationToken cancellationToken) =>
        TransientFailure.CallAsync(
            call,
            RetryPolicy.Call,
            onRetry: (exception, _) => Report(connected: false, Describe(exception)),
            cancellationToken);

    /// <summary>
    /// Tells the interface whether the server is answering, on a change and not otherwise.
    /// </summary>
    /// <remarks>
    /// Only transitions are reported. A stream backing off against a server that is down retries for
    /// as long as the player leaves it to, and one notice per attempt would be a queue the game
    /// thread drains instead of drawing.
    /// </remarks>
    private void Report(bool connected, string? detail)
    {
        var was = Interlocked.Exchange(ref _connected, connected ? 1 : 0);
        if (was == (connected ? 1 : 0)) return;
        _notices.Enqueue(new MultiplayerNotice.ConnectionChanged(connected, detail));
    }

    /// <summary>
    /// Keeps the tally of seats that have said they are done with the open turn.
    /// </summary>
    /// <remarks>
    /// A turn's readiness is forgotten when a later turn's arrives, so the count never carries over.
    /// Only seats taken at match start are counted, which is the roster the server waits on.
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
        _notices.Enqueue(new MultiplayerNotice.ReadinessChanged(
            turn, _readyPlayerIds.Count, _awaitedSeats));
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
            if (player.Slot is >= 0 and < MatchLimits.PlayerCount) slots[player.Id] = player.Slot;
        }
        return slots;
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

    private static string Describe(Exception exception) => exception switch
    {
        MultiplayerApiException { Reason: "revoked" or "invalid_token" } =>
            "You are no longer in this match.",
        MultiplayerApiException { Reason: "turn_not_open" } =>
            "That turn has already sealed.",
        MultiplayerApiException api => $"The server refused: {api.Message}",
        MultiplayerProtocolException protocol => protocol.Message,
        MultiplayerTimeoutException timeout => timeout.Message,
        _ => "The connection to the match was lost.",
    };

    private sealed record PendingTakeoverVote(string PlayerId, int Turn)
    {
        internal Dictionary<string, TakeoverChoice> Votes { get; } = new(StringComparer.Ordinal);
    }
}
