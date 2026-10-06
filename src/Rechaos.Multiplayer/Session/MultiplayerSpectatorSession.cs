using System.Net;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Follows an online match as a spectator: the city as the players had it some turns ago, rebuilt
/// from what the server has released.
/// </summary>
/// <remarks>
/// <para>
/// The server holds every spectator read behind the match's delay, so this session never sees a
/// turn the players are still planning or one they sealed less than the delay ago. It starts from
/// the newest snapshot at or below the released turn and plays the released sealed sets on top of
/// it with the same <see cref="SealedTurnApplier"/> the players use, so the state it shows is the
/// state every player's client reached on that turn.
/// </para>
/// <para>
/// A seat changing hands lives in the event log and nowhere in a sealed set. The spectator log
/// carries those events and the turn markers between them, cut off at the seal of the first turn not
/// yet released, and this session replays them in log order exactly as a reconnecting player does:
/// a handover takes effect before the turn the log was on when it was announced.
/// </para>
/// <para>
/// Nothing here writes to the match. The session is driven by its caller, one call at a time:
/// <see cref="PollAsync"/> is not safe to call concurrently with itself.
/// </para>
/// </remarks>
public sealed class MultiplayerSpectatorSession
{
    /// <summary>How many events one page asks for: the server's own page size.</summary>
    private const int EventPageSize = 200;

    private readonly SpectatorHandle _spectator;
    private readonly OriginalData _definitions;

    /// <summary>The seat of every player who ever held one, by player id.</summary>
    /// <remarks>
    /// Keyed by player, so a slot that several rows have held over the match (a seat taken over and
    /// later joined by somebody new) maps each of them to the same seat.
    /// </remarks>
    private readonly Dictionary<string, int> _slotsByPlayerId = new(StringComparer.Ordinal);

    private MatchReplayRecorder? _replay;

    /// <summary>The spectator log's cursor: every event at or before it has been applied.</summary>
    private int _cursor;

    /// <summary>
    /// The turn the log is on at the event being applied: one past the last turn it sealed. See the
    /// member session's history turn, which this mirrors.
    /// </summary>
    private int _historyTurn = 1;

    private MultiplayerSpectatorSession(
        SpectatorHandle spectator,
        OriginalData definitions,
        SpectatorMatchView view)
    {
        _spectator = spectator;
        _definitions = definitions;
        View = view;
    }

    /// <summary>
    /// Reads the match and, when the server has released a starting point, the state to show.
    /// </summary>
    /// <param name="spectator">The handle a spectator token was bound to.</param>
    /// <param name="definitions">The original data the match was generated from.</param>
    public static async Task<MultiplayerSpectatorSession> StartAsync(
        SpectatorHandle spectator,
        OriginalData definitions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spectator);
        ArgumentNullException.ThrowIfNull(definitions);
        var view = await spectator.GetAsync(cancellationToken).ConfigureAwait(false);
        var session = new MultiplayerSpectatorSession(spectator, definitions, view);
        await session.CatchUpAsync(view, cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <summary>The match as the server last described it to this spectator.</summary>
    public SpectatorMatchView View { get; private set; }

    /// <summary>Whether the session has a state to show yet.</summary>
    /// <remarks>
    /// False in the lobby, and on a running match whose bootstrap snapshot the host has not
    /// uploaded yet.
    /// </remarks>
    public bool HasState => _replay is not null;

    /// <summary>The last turn the shown state has resolved, or null before there is a state.</summary>
    public int? ShownTurn => _replay is null
        ? null
        : _replay.State.Outcome is null ? _replay.State.Coordinator.Turn - 1 : LastAppliedTurn;

    /// <summary>The last sealed turn applied, or the snapshot's turn when none has been.</summary>
    private int LastAppliedTurn { get; set; }

    /// <summary>
    /// Whether nothing more will be released: the match is over and every released turn is shown.
    /// </summary>
    public bool IsComplete =>
        View.Status is MatchStatus.Finished or MatchStatus.Abandoned
        && (_replay is null ? View.ReleasedTurn == 0 : LastAppliedTurn >= View.ReleasedTurn);

    /// <summary>
    /// A copy of the shown state for the interface to draw, or null before there is one.
    /// </summary>
    /// <remarks>
    /// A copy, never the session's own state: the interface reads it on the game thread while the
    /// next poll may be applying a turn to the original.
    /// </remarks>
    public MatchState? CloneState() =>
        _replay is null ? null : MatchStateClone.Of(_replay.State, _definitions);

    /// <summary>
    /// Asks the server what has been released since the last call, and applies it.
    /// </summary>
    /// <returns>True when the shown state moved: a first state, or one or more turns applied.</returns>
    public async Task<bool> PollAsync(CancellationToken cancellationToken)
    {
        var view = await _spectator.GetAsync(cancellationToken).ConfigureAwait(false);
        return await CatchUpAsync(view, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops watching. The token stops working on the server.</summary>
    public Task LeaveAsync(CancellationToken cancellationToken) =>
        _spectator.LeaveAsync(cancellationToken);

    private async Task<bool> CatchUpAsync(SpectatorMatchView view, CancellationToken cancellationToken)
    {
        RequireWatchable(view);
        View = view;
        var moved = false;
        if (_replay is null)
        {
            if (view.Status == MatchStatus.Lobby) return false;
            var snapshot = await ReleasedSnapshotOrNullAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is null) return false;
            Adopt(snapshot, view.ReleasedTurn);
            moved = true;
        }
        var before = LastAppliedTurn;
        await ReplayReleasedEventsAsync(cancellationToken).ConfigureAwait(false);
        return moved || LastAppliedTurn != before;
    }

    /// <summary>
    /// Refuses a match this build cannot rebuild, before a byte of its state is read.
    /// </summary>
    private static void RequireWatchable(SpectatorMatchView view)
    {
        if (view.SessionVersion != MultiplayerSessionVersion.Current)
        {
            throw new MultiplayerProtocolException(
                $"the match is session version {view.SessionVersion}, but this build plays "
                + MultiplayerSessionVersion.Current);
        }
        if (view.ReleasedTurn < 0 || view.ReleasedTurn > Math.Max(0, view.CurrentTurn))
        {
            throw new MultiplayerProtocolException(
                $"the server released turn {view.ReleasedTurn} of a match on turn {view.CurrentTurn}");
        }
    }

    /// <summary>The newest released snapshot, or null while the server has none to give.</summary>
    private async Task<SnapshotView?> ReleasedSnapshotOrNullAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _spectator.LatestSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MultiplayerApiException refusal)
            when (refusal.Status == HttpStatusCode.NotFound && refusal.Reason == "no_snapshot")
        {
            return null;
        }
    }

    /// <summary>
    /// Takes a released snapshot as the starting state, after checking it is one.
    /// </summary>
    /// <remarks>
    /// The same checks a resuming player makes, with the delay added: a snapshot past the released
    /// turn is a server handing a spectator what the players are still playing.
    /// </remarks>
    private void Adopt(SnapshotView snapshot, int releasedTurn)
    {
        if (snapshot.Turn < 0 || snapshot.Turn > releasedTurn)
        {
            throw new MultiplayerProtocolException(
                $"the server offered the snapshot for turn {snapshot.Turn} with turn {releasedTurn} released");
        }
        if (snapshot.SessionVersion != MultiplayerSessionVersion.Current)
        {
            throw new MultiplayerProtocolException(
                $"the snapshot is session version {snapshot.SessionVersion}, but this build plays "
                + MultiplayerSessionVersion.Current);
        }
        if (snapshot.FormatVersion > NativeSaveSerializer.CurrentFormatVersion)
        {
            throw new MultiplayerProtocolException(
                $"the snapshot for turn {snapshot.Turn} is save format {snapshot.FormatVersion}, and "
                + $"this build reads up to {NativeSaveSerializer.CurrentFormatVersion}");
        }
        MatchState restored;
        try
        {
            restored = MatchStateClone.FromBase64(snapshot.Body, _definitions);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            throw new MultiplayerProtocolException(
                $"the snapshot for turn {snapshot.Turn} is not a match this build can read: {exception.Message}",
                exception);
        }
        if (!string.Equals(
                MatchStateHasher.ComputeFingerprint(restored), snapshot.StateHash, StringComparison.Ordinal))
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
        LastAppliedTurn = snapshot.Turn;
        foreach (var player in View.Players)
        {
            if (MatchBootstrapFactory.IsSeated(player)) _slotsByPlayerId[player.Id] = player.Slot;
        }
    }

    /// <summary>
    /// Reads the released log from the cursor and applies it in order, until the server has nothing
    /// more to give.
    /// </summary>
    /// <remarks>
    /// The server pages by its own scan and returns where the next page starts; a page can be empty
    /// and still move the cursor past events a spectator does not see. It stops moving at the seal
    /// of the first turn not yet released, which is where this stops too.
    /// </remarks>
    private async Task ReplayReleasedEventsAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var page = await _spectator.EventsAsync(_cursor, EventPageSize, cancellationToken)
                .ConfigureAwait(false);
            foreach (var @event in page.Events)
            {
                if (@event.Seq <= _cursor)
                {
                    throw new MultiplayerProtocolException(
                        $"the spectator log went back to event {@event.Seq} after {_cursor}");
                }
                await ApplyEventAsync(@event, cancellationToken).ConfigureAwait(false);
                _cursor = @event.Seq;
            }
            if (page.Cursor <= _cursor) return;
            _cursor = page.Cursor;
        }
    }

    private async Task ApplyEventAsync(MatchEvent @event, CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case MatchPlayerTakenOverEvent takenOver:
                HandOverSeat(takenOver.Payload.PlayerId, PlayerController.Computer);
                return;
            case MatchPlayerReturnedEvent { Payload.ReplacedComputer: true } returned:
                HandOverSeat(returned.Payload.PlayerId, PlayerController.Human);
                return;
            case MatchLatePlayerJoinedEvent joined:
                if (joined.Payload.Slot is >= 0 and < MatchLimits.PlayerCount)
                    _slotsByPlayerId[joined.Payload.PlayerId] = joined.Payload.Slot;
                HandOverSeat(joined.Payload.PlayerId, PlayerController.Human);
                return;
            case TurnOpenedEvent opened:
                _historyTurn = Math.Max(_historyTurn, opened.Payload.Turn);
                return;
            case TurnSealedEvent sealedTurn:
                _historyTurn = Math.Max(_historyTurn, sealedTurn.Payload.Turn + 1);
                await ApplySealedTurnAsync(
                    sealedTurn.Payload.Turn, sealedTurn.Payload.OrderSetHash, cancellationToken)
                    .ConfigureAwait(false);
                return;
        }
    }

    /// <summary>
    /// Hands a seat over before the turn the log is on, unless the state already stands past it.
    /// </summary>
    /// <remarks>
    /// A handover before turn N is part of any state on a later turn, so one met while replaying the
    /// log up to the snapshot is already in it. A finished match ignores the transfer, as it does for
    /// the players.
    /// </remarks>
    private void HandOverSeat(string playerId, PlayerController controller)
    {
        var replay = _replay!;
        if (!_slotsByPlayerId.TryGetValue(playerId, out var slot)) return;
        if (_historyTurn < replay.State.Coordinator.Turn || replay.State.Outcome is not null) return;
        var player = replay.State.FindPlayer(new PlayerId(slot));
        if (player is null || player.Setup.Controller == controller) return;
        if (controller == PlayerController.Computer)
            replay.TransferPlayerToComputer(player.Id);
        else
            replay.TransferPlayerToHuman(player.Id);
    }

    /// <summary>Fetches, verifies and applies a released turn, unless the state already holds it.</summary>
    private async Task ApplySealedTurnAsync(
        int turn,
        string announcedOrderSetHash,
        CancellationToken cancellationToken)
    {
        var replay = _replay!;
        if (turn <= LastAppliedTurn) return;
        if (replay.State.Outcome is not null)
        {
            throw new MultiplayerProtocolException(
                $"the log sealed turn {turn} after the match had ended on turn {LastAppliedTurn}");
        }
        if (turn != replay.State.Coordinator.Turn)
        {
            throw new MultiplayerProtocolException(
                $"the log sealed turn {turn} while the match was on turn {replay.State.Coordinator.Turn}");
        }
        var sealedOrders = await _spectator.SealedOrdersAsync(turn, cancellationToken)
            .ConfigureAwait(false);
        if (sealedOrders.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s sealed set with the set for turn {sealedOrders.Turn}");
        }
        if (!string.Equals(sealedOrders.OrderSetHash, announcedOrderSetHash, StringComparison.Ordinal)
            || !OrderDigest.Verifies(sealedOrders, announcedOrderSetHash))
        {
            throw new MultiplayerProtocolException(
                $"the sealed set for turn {turn} does not match the digest the log announced");
        }
        SealedTurnApplier.Apply(replay, sealedOrders);
        LastAppliedTurn = turn;
    }
}
