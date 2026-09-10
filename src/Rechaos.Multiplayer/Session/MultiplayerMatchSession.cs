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
/// Everything the protocol does happens on one background task, so the ordering of applied turns is
/// the ordering the log delivered. The game loop only ever queues a submission and drains notices.
/// </para>
/// </remarks>
public sealed class MultiplayerMatchSession : IAsyncDisposable
{
    private readonly MatchHandle _match;
    private readonly OriginalData _definitions;
    private readonly IReadOnlySet<int> _humanSlots;
    private readonly ConcurrentQueue<MultiplayerNotice> _notices = new();
    private readonly CancellationTokenSource _stopping = new();
    private MatchReplayRecorder _replay;
    private Task? _pump;
    private int _resumeAfterSeq;

    private MultiplayerMatchSession(
        MultiplayerSessionOptions options,
        MatchReplayRecorder replay,
        PlayerView self,
        IReadOnlySet<int> humanSlots)
    {
        _match = options.Match;
        _definitions = options.Definitions;
        _replay = replay;
        _humanSlots = humanSlots;
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
        var settings = MultiplayerGameSettings.FromWire(view.Settings.GameSettings);
        var state = MatchBootstrapFactory.Create(options.Definitions, seed, settings, view.Players);
        var replay = new MatchReplayRecorder(state);
        CommandPhase.Enter(replay);

        var session = new MultiplayerMatchSession(
            options, replay, self, MatchBootstrapFactory.HumanSlots(view.Players));
        session.InitialState = MatchStateClone.Of(state, options.Definitions);
        session._pump = Task.Run(() => session.PumpAsync(session._stopping.Token));
        return session;
    }

    /// <summary>The next thing the interface should know about, if anything is waiting.</summary>
    public bool TryDequeueNotice(out MultiplayerNotice notice) => _notices.TryDequeue(out notice!);

    /// <summary>
    /// Sends this player's order document for a turn, with readiness.
    /// </summary>
    /// <remarks>
    /// The whole document goes every time, not a delta: the server replaces what it held, and the
    /// turn seals in this same call when readiness completes the roster. A submission that lands
    /// after the seal is refused rather than folded in.
    /// </remarks>
    public async Task<OwnSubmissionView> SubmitOrdersAsync(
        int turn,
        OrderDocument document,
        bool ready,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return await _match
            .SubmitOrdersAsync(turn, new SubmitOrdersRequest(document, ready), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gives up the seat. The slot becomes a computer player from the next turn.</summary>
    public Task LeaveAsync(CancellationToken cancellationToken) =>
        _match.LeaveAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_pump is not null)
        {
            try
            {
                await _pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopping is how a session ends; the pump is meant to be cancelled.
            }
        }
        _stopping.Dispose();
    }

    /// <summary>
    /// Reads the log forever, acting on every fact.
    /// </summary>
    /// <remarks>
    /// A failure that ends the stream ends the session with it: a revoked token means the player
    /// left or was kicked, and there is nothing left to read. Anything else is retried by
    /// <see cref="MatchEventStream"/> itself.
    /// </remarks>
    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        var stream = new MatchEventStream(_match);
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
            case MatchStatusChangedEvent status:
                if (status.Payload.Status == MatchStatus.Finished)
                {
                    _notices.Enqueue(new MultiplayerNotice.MatchFinished());
                }
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                return;
            case LobbyPlayerLeftEvent or LobbyPlayerJoinedEvent or LobbyHostChangedEvent:
                await PublishMatchAsync(cancellationToken).ConfigureAwait(false);
                return;
            default:
                // Readiness and confirmation are facts the match view already carries, and a turn
                // this client has applied needs nothing more said about it.
                return;
        }
    }

    /// <summary>
    /// Fetches a sealed set, checks it against the digest, applies it and reports the result.
    /// </summary>
    /// <remarks>
    /// Delivery is at least once, so a seal for a turn this client has already resolved is dropped
    /// rather than applied twice: applying turn N leaves the coordinator on N+1, which is the test.
    /// </remarks>
    private async Task ResolveSealedTurnAsync(int turn, CancellationToken cancellationToken)
    {
        if (turn < _replay.State.Coordinator.Turn) return;
        var sealedOrders = await _match.SealedOrdersAsync(turn, cancellationToken).ConfigureAwait(false);
        if (!OrderDigest.Verifies(sealedOrders, sealedOrders.OrderSetHash))
        {
            throw new MultiplayerProtocolException(
                $"the sealed set for turn {turn} does not match the digest the server announced");
        }
        var stateHash = SealedTurnApplier.Apply(_replay, sealedOrders, _humanSlots);
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
        await _match.UploadSnapshotAsync(
            new UploadSnapshotRequest(
                turn,
                MatchReplaySerializer.CurrentFormatVersion,
                ours,
                MatchStateClone.ToBase64(_replay.State)),
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
        var snapshot = await _match.SnapshotAsync(turn, cancellationToken).ConfigureAwait(false);
        var restored = MatchStateClone.FromBase64(snapshot.Body, _definitions);
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

    private Task ReportAsync(int turn, string stateHash, CancellationToken cancellationToken) =>
        _match.ReportAsync(
            turn,
            new TurnReportRequest(stateHash, _replay.State.Outcome is not null),
            cancellationToken);

    private async Task PublishMatchAsync(CancellationToken cancellationToken)
    {
        var detail = await _match.GetAsync(cancellationToken).ConfigureAwait(false);
        _notices.Enqueue(new MultiplayerNotice.MatchUpdated(detail.Match));
    }

    /// <summary>An ISO instant, or null when the turn has no deadline because there is no timer.</summary>
    private static DateTimeOffset? ParseInstant(string? value) =>
        value is null
            ? null
            : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string Describe(Exception exception) => exception switch
    {
        MultiplayerApiException { Reason: "revoked" } => "You are no longer in this match.",
        MultiplayerApiException api => $"The server refused: {api.Message}",
        MultiplayerProtocolException protocol => protocol.Message,
        _ => "The connection to the match was lost.",
    };
}
