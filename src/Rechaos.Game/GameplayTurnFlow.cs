using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>
/// Presents the original player-facing planning turn while retaining the
/// simulation's explicit deterministic resolution phases underneath it.
/// </summary>
public static class GameplayTurnFlow
{
    /// <summary>
    /// Advances to the next planning slot and reports eliminated slots crossed on the way.
    /// </summary>
    /// <remarks>
    /// The report is presentation-only. The recorded transition remains exactly the same; the
    /// local client uses its order to show the original private eliminated-player presentation
    /// before continuing to later command slots.
    /// </remarks>
    public static PlanningAdvance AdvanceToPlanning(MatchReplayRecorder replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        var crossedEliminatedPlayers = new List<PlayerId>();
        while (replay.State.Outcome is null)
        {
            var state = replay.State;
            if (state.Coordinator.Phase != TurnPhase.Command)
            {
                AdvanceAutomaticPhase(replay);
                continue;
            }

            var player = state.Coordinator.ActivePlayer!.Value;
            if (state.FindPlayer(player)?.Status == PlayerStatus.Active)
                return new PlanningAdvance(crossedEliminatedPlayers);
            crossedEliminatedPlayers.Add(player);
            replay.FinishCommand(player);
        }
        return new PlanningAdvance(crossedEliminatedPlayers);
    }

    public static PlanningAdvance FinishPlanningTurn(MatchReplayRecorder replay, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(replay);
        replay.FinishCommand(player);
        return AdvanceToPlanning(replay);
    }

    private static void AdvanceAutomaticPhase(MatchReplayRecorder replay)
    {
        var state = replay.State;
        switch (state.Coordinator.Phase)
        {
            case TurnPhase.Upkeep:
                replay.FinishUpkeep();
                break;
            case TurnPhase.Execution:
                replay.FinishExecutionPhase();
                break;
            case TurnPhase.Hire:
                replay.FinishHire(state.Coordinator.ActivePlayer!.Value);
                break;
            case TurnPhase.PlayerElimination:
                replay.FinishPlayerElimination();
                break;
            case TurnPhase.Command:
                throw new InvalidOperationException("A planning phase is not automatic.");
            default:
                throw new InvalidOperationException("Unknown turn phase.");
        }
    }
}

/// <summary>
/// The eliminated command slots crossed by one automatic advance, in native slot order.
/// </summary>
public sealed record PlanningAdvance(IReadOnlyList<PlayerId> CrossedEliminatedPlayers);
