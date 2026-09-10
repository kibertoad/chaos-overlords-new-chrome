namespace Rechaos.Core.GameModel;

internal static class AiPlanningPreparation
{
    public static void ApplyFamilyAssignments(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var gangs = state.FindPlayer(player)?.Gangs
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        state.AiPlanning.BeginPlanning(player);
        for (var gangSlot = 0; gangSlot < gangs.Count; gangSlot++)
        {
            if (!gangs[gangSlot].IsActive) continue;
            var selection = OriginalAiFamilyRules.Select(
                state.Setup.Scenario,
                state.AiPlanning.CurrentHireRole(player),
                state.AiPlanning.Family(player, gangSlot));
            state.AiPlanning.SetFamily(player, gangSlot, selection.Family);
        }
    }
}
