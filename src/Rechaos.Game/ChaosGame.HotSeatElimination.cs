using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void QueueHotSeatEliminations(PlanningAdvance advance)
    {
        if (_session is not null || _state is not { Outcome: null } state) return;
        var humans = advance.CrossedEliminatedPlayers.Where(playerId =>
            state.FindPlayer(playerId)?.Setup.Controller == PlayerController.Human);
        foreach (var playerId in HotSeatEliminationPresentation.QueueUnpresented(
                     humans, _presentedHotSeatEliminations))
            _pendingHotSeatEliminations.Enqueue(playerId);
    }

    private void ResetHotSeatEliminationPresentation(bool acknowledgeExistingEliminations)
    {
        _pendingHotSeatEliminations.Clear();
        _presentedHotSeatEliminations.Clear();
        _eliminationHandoffPlayer = null;
        _eliminationMusicHeld = false;
        if (!acknowledgeExistingEliminations || _state is null) return;
        foreach (var player in _state.Players.Where(player =>
                     player.Setup.Controller == PlayerController.Human
                     && player.Status == PlayerStatus.Eliminated))
            _presentedHotSeatEliminations.Add(player.Id);
    }

    /// <summary>
    /// RULE-OBJECTIVE-005: the next elimination card, behind the Ready card only when more than one
    /// local human is counted this round.
    /// </summary>
    private bool ShowPendingHotSeatElimination()
    {
        if (!_pendingHotSeatEliminations.TryDequeue(out var playerId)) return false;
        _eliminationHandoffPlayer = playerId;
        _screens.Show(_state is not null && HotSeatHandoffPresentation.RequiresPrivateHandoff(_state)
            ? ClientScreen.Handoff
            : ClientScreen.Elimination);
        return true;
    }

    /// <summary>
    /// Shows the end of a finished match. RULE-OBJECTIVE-005: a local game whose humans are all
    /// out returns to the title without the awards.
    /// </summary>
    private void ShowMatchEnd()
    {
        if (_session is null && _state?.Outcome?.Reason == MatchEndReason.NoHumansLeft)
        {
            LeaveEndgame();
            return;
        }
        _screens.Show(ClientScreen.Endgame);
    }

    private void HandleHotSeatEliminationClick(Point point)
    {
        if (!EndgameLayout.Done.Contains(point)) return;
        PlayGeneralSound(AudioRouting.PointerPushSound());
        FinishHotSeatEliminationPresentation();
    }

    private void FinishHotSeatEliminationPresentation()
    {
        // RULE-AUDIO-001: the card started the endgame music, and only a local human after this
        // slot in slot order asks for the gameplay music again.
        if (_eliminationHandoffPlayer is { } shown && _state is not null)
            _eliminationMusicHeld = !HotSeatEliminationPresentation.HasLaterLocalHuman(_state, shown);
        _eliminationHandoffPlayer = null;
        if (ShowPendingHotSeatElimination()) return;

        if (_state?.Coordinator.ActivePlayer is { } playerId
            && _state.FindPlayer(playerId)?.Setup.Controller == PlayerController.Human)
        {
            PresentHotSeatPlanningEntry();
            return;
        }

        _screens.Show(ClientScreen.City);
    }

    private void DrawHotSeatElimination(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_eliminationHandoffPlayer is not { } playerId
            || state.FindPlayer(playerId) is not { } player)
        {
            _screens.Show(ClientScreen.City);
            return;
        }

        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        DrawEndgameBackground(batch, pixel);
        DrawEndgameNoticeCard(batch, pixel, font, _eliminationBackground, playerId,
            player.Setup.PortraitId, player.Setup.Name);
    }
}

/// <summary>
/// Keeps an eliminated local seat's private presentation one-shot while the coordinator continues
/// to cross its permanently retired command slot on later rounds.
/// </summary>
public static class HotSeatEliminationPresentation
{
    public static IReadOnlyList<PlayerId> QueueUnpresented(
        IEnumerable<PlayerId> crossedEliminatedPlayers,
        ISet<PlayerId> presentedPlayers)
    {
        ArgumentNullException.ThrowIfNull(crossedEliminatedPlayers);
        ArgumentNullException.ThrowIfNull(presentedPlayers);
        var queued = new List<PlayerId>();
        foreach (var playerId in crossedEliminatedPlayers)
            if (presentedPlayers.Add(playerId)) queued.Add(playerId);
        return queued;
    }

    /// <summary>
    /// RULE-OBJECTIVE-005: whether a local human still playing sits after the given slot, which is
    /// what brings the gameplay music back after an elimination card.
    /// </summary>
    public static bool HasLaterLocalHuman(MatchState state, PlayerId eliminated)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Players.Any(player => player.Id.Value > eliminated.Value
            && player.Setup.Controller == PlayerController.Human
            && player.Status == PlayerStatus.Active);
    }
}

/// <summary>
/// Mirrors the original handoff gate (RULE-OBJECTIVE-005). Computer opponents do not make a solo
/// local game private. A local human eliminated in the turn before this one still counts, since
/// its card is shown this round; one eliminated earlier has had its card and no longer counts.
/// </summary>
public static class HotSeatHandoffPresentation
{
    public static bool RequiresPrivateHandoff(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var lastRound = state.Coordinator.Turn - 1;
        var eliminatedLastRound = state.Events
            .Where(gameEvent => gameEvent.Kind == GameEventKind.PlayerEliminated
                && gameEvent.Turn >= lastRound)
            .Select(gameEvent => gameEvent.Player)
            .ToHashSet();
        return state.Players.Count(player => player.Setup.Controller == PlayerController.Human
            && (player.Status == PlayerStatus.Active || eliminatedLastRound.Contains(player.Id))) > 1;
    }
}
