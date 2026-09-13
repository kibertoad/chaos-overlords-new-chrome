namespace Rechaos.Core.GameModel;

/// <summary>
/// Keeps recreation-native AI improvements outside the recovered Original AI
/// planner so selecting Advanced cannot change Original decision fixtures.
/// </summary>
public static class AiPolicyPlanner
{
    public static IReadOnlyList<GameCommand> Plan(MatchState state, PlayerId player) =>
        state.Setup.AiPolicy switch
        {
            AiPolicyMode.Original => AiTurnPlanner.Plan(state, player),
            AiPolicyMode.Advanced => AiTurnPlanner.PlanAdvanced(state, player),
            _ => throw new InvalidOperationException("The match has an unknown AI policy.")
        };
}
