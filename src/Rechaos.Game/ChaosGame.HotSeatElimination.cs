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
        ClearFinalViews();
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
    /// out returns to the title without the awards. Otherwise a match that has just ended gives
    /// each local human its final view before the awards (FND-OBJECTIVE-004); a finished match
    /// that was loaded opens on the awards.
    /// </summary>
    private void ShowMatchEnd(bool justEnded = true)
    {
        if (_session is null && _state?.Outcome?.Reason == MatchEndReason.NoHumansLeft)
        {
            LeaveEndgame();
            return;
        }
        if (_session is null && justEnded)
        {
            BeginFinalViews();
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
        if (_finalViewPlayer is not null)
        {
            ShowNextFinalView();
            return;
        }
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

        // EXP-UI-018: the screen around the frame is black when the card follows the turn's
        // resolution.
        batch.Draw(pixel, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height), Color.Black);
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
        var activeHumans = state.Players.Count(player =>
            player.Setup.Controller == PlayerController.Human && player.Status == PlayerStatus.Active);
        if (activeHumans > 1) return true;

        var counted = activeHumans;
        foreach (var playerId in PlayersEliminatedSince(state, state.Coordinator.Turn - 1))
        {
            if (state.FindPlayer(playerId) is { Status: PlayerStatus.Eliminated } player
                && player.Setup.Controller == PlayerController.Human
                && ++counted > 1) return true;
        }
        return false;
    }

    /// <summary>
    /// The players eliminated on or after <paramref name="turn"/>. Events are appended in turn
    /// order, so the scan walks back from the newest and stops at the first older event instead
    /// of reading the whole match history on every handoff.
    /// </summary>
    private static IEnumerable<PlayerId> PlayersEliminatedSince(MatchState state, int turn)
    {
        var events = state.Events;
        for (var index = events.Count - 1; index >= 0 && events[index].Turn >= turn; index--)
        {
            if (events[index].Kind == GameEventKind.PlayerEliminated) yield return events[index].Player;
        }
    }
}
