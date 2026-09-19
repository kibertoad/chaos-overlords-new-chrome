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
/// through the notice queue, which the game thread drains, and either one failing for good stops
/// the other: there is no half-alive session that reads turns it can no longer answer.
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
    private Task? _disposal;
    private int _failed;

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
        session._pump = Task.Run(() => session.RunPumpAsync(session._stoppingToken));
        session._outbox = Task.Run(() => session.RunOutboxAsync(session._stoppingToken));
        return session;
    }

    /// <summary>The next thing the interface should know about, if anything is waiting.</summary>
    public bool TryDequeueNotice(out MultiplayerNotice notice) => _notices.TryDequeue(out notice!);

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
    /// Ends the session over something it cannot recover from, once.
    /// </summary>
    /// <remarks>
    /// Either loop can be the one that finds out — a revoked token answers the outbox as readily
    /// as the pump — and whichever does, the other is stopped with it. One <see cref="MultiplayerNotice.Failed"/>
    /// reaches the interface: the second loop ends by cancellation, which is not a failure of its own.
    /// </remarks>
    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _failed, 1) != 0) return;
        _notices.Enqueue(new MultiplayerNotice.Failed(Describe(exception), exception));
        _stopping.Cancel();
    }

    /// <summary>The pump's whole life: reconstruct if the match moved on, then read the log forever.</summary>
    private async Task RunPumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsRestoring && !await RestoreAsync(replayFromSeq: 0, cancellationToken).ConfigureAwait(false))
                return;
            await PumpAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ordinary shutdown, or the outbox failed first and stopped the session.
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    /// <summary>
    /// Reads the log forever, acting on every fact in the order the log gives them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A failure that ends the stream ends the session with it: a revoked token means the player
    /// left or was kicked, and there is nothing left to read. Everything that describes one attempt
    /// is retried — the stream by itself, and the calls a fact leads to by <see cref="CallAsync"/>.
    /// </para>
    /// <para>
    /// Sequence numbers are gapless, so the stream is held to that: an event that skips past the
    /// next expected number means something between was never delivered, and acting on what came
    /// after would apply a later turn to an earlier state. The session resynchronises instead — the
    /// same reconstruction a restart does — and resumes from where the server now is. An event at
    /// or below the last applied number is the at-least-once repeat and is dropped.
    /// </para>
    /// </remarks>
    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        if (_uploadInitialSnapshot)
        {
            await UploadInitialSnapshotAsync(cancellationToken).ConfigureAwait(false);
            _uploadInitialSnapshot = false;
        }
        while (true)
        {
            var stream = new MatchEventStream(
                _match,
                RetryPolicy.Stream,
                onReconnect: (exception, attempt) => _streamLane.Failed(Describe(exception), attempt),
                onConnected: _streamLane.Recovered,
                _streamIdleTimeout);
            var gap = false;
            await foreach (var @event in stream
                .ReadAsync(_resumeAfterSeq, cancellationToken).ConfigureAwait(false))
            {
                if (!string.Equals(@event.MatchId, _match.MatchId, StringComparison.Ordinal))
                {
                    throw new MultiplayerProtocolException(
                        $"event sequence {@event.Seq} belongs to another match");
                }
                var expected = _resumeAfterSeq + 1;
                if (@event.Seq < expected) continue;
                if (@event.Seq > expected)
                {
                    _streamLane.Failed(
                        $"The event stream jumped from sequence {_resumeAfterSeq} to {@event.Seq}; "
                        + "resynchronising with the server.",
                        attempt: 1);
                    gap = true;
                    break;
                }
                await HandleAsync(@event, cancellationToken).ConfigureAwait(false);
                _resumeAfterSeq = @event.Seq;
            }
            // The stream ends only by throwing, by cancellation, or by the gap above.
            if (!gap) return;
            if (!await RestoreAsync(_resumeAfterSeq, cancellationToken).ConfigureAwait(false)) return;
            _streamLane.Recovered();
        }
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
        var (stateHash, includedOwnOrders) = await FetchAndApplySealedTurnAsync(
                turn, announcedOrderSetHash, cancellationToken)
            .ConfigureAwait(false);
        await ReportAsync(turn, stateHash, cancellationToken).ConfigureAwait(false);
        _notices.Enqueue(new MultiplayerNotice.TurnResolved(
            turn, MatchStateClone.Of(_replay.State, _definitions), stateHash, includedOwnOrders));
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
        var sealedOrders = await CallAsync(
            token => _match.SealedOrdersAsync(turn, token), _pumpLane, cancellationToken)
            .ConfigureAwait(false);
        if (sealedOrders.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s sealed set with the set for turn {sealedOrders.Turn}");
        }
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
        var includedOwnOrders = sealedOrders.Players.Any(entry => entry.Slot == Slot);
        return (SealedTurnApplier.Apply(_replay, sealedOrders), includedOwnOrders);
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
        var details = string.Join(", ", desynced.Payload.Reports
            .OrderBy(report => report.PlayerId, StringComparer.Ordinal)
            .Select(report => $"{report.PlayerId}:{ShortHash(report.StateHash)}"));
        _notices.Enqueue(new MultiplayerNotice.Desynced(
            turn,
            IsHost,
            canRepair,
            $"LOCAL {ShortHash(ours)}  REPORTS {details}"));
        if (!canRepair) return;
        await CallAsync(
            token => _match.UploadSnapshotAsync(
                new UploadSnapshotRequest(
                    turn,
                    // The body is a native save, so the version that describes it is the native
                    // save format's — not the replay format's, which says nothing about these bytes.
                    NativeSaveSerializer.CurrentFormatVersion,
                    MultiplayerProtocolVersion.Current,
                    MultiplayerSessionVersion.Current,
                    ours,
                    MatchStateClone.ToBase64(_replay.State),
                    SummarizeSeats(_replay.State)),
                token),
            _pumpLane,
            cancellationToken).ConfigureAwait(false);

        static string ShortHash(string hash) => hash[..Math.Min(12, hash.Length)];
    }

    /// <summary>
    /// Adopts a repaired state and re-reports the turn it settles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host is already on the state it uploaded, so it has nothing to load and nothing new to
    /// say; every other client replaces its own with the snapshot, recomputes the hash and reports
    /// again. Loading also starts a fresh recorder: the old one is bound to the state it was
    /// constructed over and refuses to record another.
    /// </para>
    /// <para>
    /// A repair for a turn older than the one this client last resolved is left alone. Delivery
    /// is at least once, so the repeat of a repair already adopted arrives here, and adopting it
    /// again would throw away every turn applied since.
    /// </para>
    /// </remarks>
    private async Task AdoptSnapshotAsync(
        SnapshotAvailableEventPayload announced,
        CancellationToken cancellationToken)
    {
        var turn = announced.Turn;
        if (turn < _replay.State.Coordinator.Turn - 1) return;
        if (string.Equals(announced.UploadedByPlayerId, PlayerId, StringComparison.Ordinal)
            && string.Equals(
                announced.StateHash, MatchStateHasher.ComputeSha256(_replay.State), StringComparison.Ordinal))
        {
            return;
        }
        var snapshot = await CallAsync(
            token => _match.SnapshotAsync(turn, token), _pumpLane, cancellationToken).ConfigureAwait(false);
        if (snapshot.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s repair with the snapshot for turn {snapshot.Turn}");
        }
        var restored = ReadVerifiedSnapshot(snapshot);
        _replay = new MatchReplayRecorder(restored);
        var stateHash = snapshot.StateHash;
        await ReportAsync(turn, stateHash, cancellationToken).ConfigureAwait(false);
        _notices.Enqueue(new MultiplayerNotice.Resynced(
            turn, MatchStateClone.Of(restored, _definitions), stateHash));
    }

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
        var stateHash = MatchStateHasher.ComputeSha256(restored);
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

    private Task ReportAsync(int turn, string stateHash, CancellationToken cancellationToken) =>
        CallAsync(
            token => _match.ReportAsync(
                turn,
                new TurnReportRequest(
                    stateHash,
                    _replay.State.Outcome is not null,
                    IsHost ? SummarizeSeats(_replay.State) : null),
                token),
            _pumpLane,
            cancellationToken);

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
            .Where(player => player.Slot is >= 0 and < MatchLimits.PlayerCount
                && IsAwaitedHuman(player))
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
        CancellationToken cancellationToken)
    {
        var result = await TransientFailure.CallAsync(
            call,
            RetryPolicy.Call,
            onRetry: lane is null
                ? null
                : (exception, attempt) => lane.Failed(Describe(exception), attempt),
            cancellationToken).ConfigureAwait(false);
        lane?.Recovered();
        return result;
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

    private static string Describe(Exception exception) => MultiplayerFailureText.Describe(exception);

    private sealed record PendingTakeoverVote(string PlayerId, int Turn)
    {
        internal Dictionary<string, TakeoverChoice> Votes { get; } = new(StringComparer.Ordinal);
    }
}
