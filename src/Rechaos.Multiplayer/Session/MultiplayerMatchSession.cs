using System.Collections.Concurrent;
using System.Globalization;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>What a session needs to start driving a match that has already been seated.</summary>
/// <param name="Match">The token-bound handle for this player.</param>
/// <param name="Definitions">The bundled gameplay tables the city is generated from.</param>
/// <param name="View">The match as the server last described it; the seed and roster come from it.</param>
/// <param name="OwnPlayerId">This client's player id, for reading its own row out of the roster.</param>
/// <param name="ResumeAfterSeq">
/// The last event sequence this client has already handled. Delivery is at least once, so resuming
/// from it may repeat facts already applied; every handler here is idempotent for that reason.
/// </param>
public sealed record MultiplayerSessionOptions(
    MatchHandle Match,
    OriginalData Definitions,
    MatchView View,
    string OwnPlayerId,
    int ResumeAfterSeq);

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
public sealed class MultiplayerMatchSession : IAsyncDisposable
{
    private readonly MatchHandle _match;
    private readonly OriginalData _definitions;
    private readonly ConcurrentQueue<MultiplayerNotice> _notices = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Dictionary<string, int> _slotsByPlayerId;
    private readonly int _activeSeats;

    /// <summary>Guards <see cref="_pending"/>, which the game thread writes and the outbox reads.</summary>
    private readonly object _outboxGate = new();

    /// <summary>Wakes the outbox. Counting, so a spurious wake costs one re-check of nothing.</summary>
    private readonly SemaphoreSlim _outboxSignal = new(0);

    /// <summary>Seats that have said they are done with <see cref="_readinessTurn"/>.</summary>
    private readonly HashSet<string> _readyPlayerIds = new(StringComparer.Ordinal);

    private PendingOrders? _pending;
    private MatchReplayRecorder _replay;
    private Task? _pump;
    private Task? _outbox;
    private int _resumeAfterSeq;
    private int _readinessTurn = -1;

    /// <summary>
    /// 1 while the server is answering, 0 while it is not.
    /// </summary>
    /// <remarks>
    /// An <c>int</c> through <see cref="Interlocked"/> because both background tasks report into it:
    /// the pump when the stream drops, and the outbox when a submission needs another attempt.
    /// </remarks>
    private int _connected = 1;

    private MultiplayerMatchSession(
        MultiplayerSessionOptions options,
        MatchReplayRecorder replay,
        PlayerView self,
        Dictionary<string, int> slotsByPlayerId)
    {
        _match = options.Match;
        _definitions = options.Definitions;
        _replay = replay;
        _slotsByPlayerId = slotsByPlayerId;
        _activeSeats = slotsByPlayerId.Count;
        _resumeAfterSeq = options.ResumeAfterSeq;
        PlayerId = self.Id;
        Slot = self.Slot;
        IsHost = self.IsHost;
    }

    /// <summary>This client's player id.</summary>
    public string PlayerId { get; }

    /// <summary>The seat this client plays. Every op it records names this slot.</summary>
    public int Slot { get; }

    /// <summary>Whether this client is the one that repairs a desync by uploading a snapshot.</summary>
    public bool IsHost { get; }

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
        var state = MatchBootstrapFactory.Create(options.Definitions, seed, settings, view.Players);
        var replay = new MatchReplayRecorder(state);
        CommandPhase.Enter(replay);

        var session = new MultiplayerMatchSession(options, replay, self, SeatedSlots(view.Players));
        session.InitialState = MatchStateClone.Of(state, options.Definitions);
        session.InitialDeadline = ParseInstant(view.Turn?.DeadlineAt);
        session._pump = Task.Run(() => session.PumpAsync(session._stopping.Token));
        session._outbox = Task.Run(() => session.DrainOutboxAsync(session._stopping.Token));
        return session;
    }

    /// <summary>The next thing the interface should know about, if anything is waiting.</summary>
    public bool TryDequeueNotice(out MultiplayerNotice notice) => _notices.TryDequeue(out notice!);

    /// <summary>
    /// Queues this player's order document for a turn, to be sent off the caller's thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a game loop, which cannot wait on a round trip. The answer arrives as
    /// <see cref="MultiplayerNotice.OrdersAccepted"/> or
    /// <see cref="MultiplayerNotice.OrdersRefused"/>.
    /// </para>
    /// <para>
    /// Only the latest document is kept: the server replaces what it held rather than adding to it,
    /// so a document superseded before it was ever sent has nothing in it the newer one lacks.
    /// Readiness is the exception and accumulates — having said "I am done" is not something a later
    /// draft of the same turn takes back.
    /// </para>
    /// </remarks>
    public void QueueOrders(int turn, OrderDocument document, bool ready)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_outboxGate)
        {
            var carriedReady = ready
                || (_pending is { } pending && pending.Turn == turn && pending.Ready);
            _pending = new PendingOrders(turn, document, carriedReady);
        }
        _outboxSignal.Release();
    }

    /// <summary>
    /// Sends this player's order document for a turn, with readiness, and waits for the answer.
    /// </summary>
    /// <remarks>
    /// The whole document goes every time, not a delta: the server replaces what it held, and the
    /// turn seals in this same call when readiness completes the roster. A submission that lands
    /// after the seal is refused rather than folded in, so a caller that loses that race re-plans
    /// against the next turn. For the game loop use <see cref="QueueOrders"/> instead.
    /// </remarks>
    public Task<OwnSubmissionView> SubmitOrdersAsync(
        int turn,
        OrderDocument document,
        bool ready,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CallAsync(
            token => _match.SubmitOrdersAsync(
                turn, new SubmitOrdersRequest(document, ready), token),
            cancellationToken);
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
    /// A failure that ends the stream ends the session with it: a revoked token means the player left
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
    /// Sends whatever the interface last queued, one document at a time.
    /// </summary>
    /// <remarks>
    /// A refusal is reported and forgotten rather than ending the session: a turn that sealed while
    /// the player was still typing is an ordinary race, and the next turn is still theirs to play.
    /// </remarks>
    private async Task DrainOutboxAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _outboxSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                PendingOrders? next;
                lock (_outboxGate)
                {
                    next = _pending;
                    _pending = null;
                }
                if (next is null) continue;
                try
                {
                    await CallAsync(
                        token => _match.SubmitOrdersAsync(
                            next.Turn, new SubmitOrdersRequest(next.Document, next.Ready), token),
                        cancellationToken).ConfigureAwait(false);
                    _notices.Enqueue(new MultiplayerNotice.OrdersAccepted(next.Turn, next.Ready));
                }
                catch (Exception exception) when (exception is MultiplayerApiException
                    or MultiplayerProtocolException or MultiplayerTimeoutException
                    or HttpRequestException or IOException)
                {
                    _notices.Enqueue(
                        new MultiplayerNotice.OrdersRefused(next.Turn, Describe(exception)));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ordinary shutdown.
        }
    }

    private async Task HandleAsync(MatchEvent @event, CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case TurnSealedEvent sealedTurn:
                await ResolveSealedTurnAsync(sealedTurn.Payload.Turn, cancellationToken)
                    .ConfigureAwait(false);
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
            case LobbyPlayerLeftEvent or LobbyPlayerJoinedEvent or LobbyHostChangedEvent:
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
    private async Task ResolveSealedTurnAsync(int turn, CancellationToken cancellationToken)
    {
        if (turn < _replay.State.Coordinator.Turn) return;
        var sealedOrders = await CallAsync(
            token => _match.SealedOrdersAsync(turn, token), cancellationToken).ConfigureAwait(false);
        if (!OrderDigest.Verifies(sealedOrders, sealedOrders.OrderSetHash))
        {
            throw new MultiplayerProtocolException(
                $"the sealed set for turn {turn} does not match the digest the server announced");
        }
        var stateHash = SealedTurnApplier.Apply(_replay, sealedOrders);
        await ReportAsync(turn, stateHash, cancellationToken).ConfigureAwait(false);
        _notices.Enqueue(new MultiplayerNotice.TurnResolved(
            turn, MatchStateClone.Of(_replay.State, _definitions), stateHash));
    }

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
                    MatchStateClone.ToBase64(_replay.State)),
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
                new TurnReportRequest(stateHash, _replay.State.Outcome is not null),
                token),
            cancellationToken);

    private async Task PublishMatchAsync(CancellationToken cancellationToken)
    {
        var detail = await CallAsync(
            token => _match.GetAsync(token), cancellationToken).ConfigureAwait(false);
        _notices.Enqueue(new MultiplayerNotice.MatchUpdated(detail.Match));
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
            turn, _readyPlayerIds.Count, _activeSeats));
    }

    /// <summary>
    /// What a change of match status means for the player.
    /// </summary>
    /// <remarks>
    /// Abandoned is as final as finished and easier to miss: the server gives up on a match nobody is
    /// playing any more, and a client that only watched for <c>finished</c> would sit waiting for a
    /// turn that is never going to seal.
    /// </remarks>
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

    /// <summary>An order document waiting to be sent, and whether it completes the player's turn.</summary>
    private sealed record PendingOrders(int Turn, OrderDocument Document, bool Ready);
}
