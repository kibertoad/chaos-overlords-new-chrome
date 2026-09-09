using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>
/// Presents the original player-facing planning turn while retaining the
/// simulation's explicit deterministic resolution phases underneath it.
/// </summary>
public static class GameplayTurnFlow
{
    public static void AdvanceToPlanning(MatchReplayRecorder replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        while (replay.State.Outcome is null && replay.State.Coordinator.Phase != TurnPhase.Command)
            AdvanceAutomaticPhase(replay);
    }

    public static void FinishPlanningTurn(MatchReplayRecorder replay, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(replay);
        replay.FinishCommand(player);
        AdvanceToPlanning(replay);
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
