namespace Rechaos.Core.GameModel;

/// <summary>
/// Scenario/strategy dispatch recovered from the original planner at 0x00432da0.
/// This remains isolated until the per-player strategic-mode word (query 0x7c)
/// and per-gang planning records are represented by the live match model.
/// </summary>
internal static class OriginalAiFamilyRules
{
    public static OriginalAiFamilySelection Select(
        ScenarioId scenario,
        int strategicMode,
        int currentFamily)
    {
        int? family = scenario switch
        {
            ScenarioId.Greed => strategicMode switch
            {
                0 or 1 => 0, 2 => 3, 3 => 2, 4 => 6, 6 => 7, _ => null
            },
            ScenarioId.Power => strategicMode switch
            {
                0 => 0, 1 => 1, 2 => 3, 3 => 2, 4 => 6, 5 => 5, 6 => 7, _ => null
            },
            ScenarioId.Acceptance => strategicMode switch
            {
                0 or 1 => 0, 2 => 5, 3 => 2, 4 => 6, 6 => 7, _ => null
            },
            ScenarioId.Dominance => strategicMode switch
            {
                0 or 1 => 0, 2 => 5, 3 => 2, 4 => 6, 5 => 3, 6 => 7, _ => null
            },
            ScenarioId.KillEmAll or ScenarioId.Big40 => strategicMode switch
            {
                0 => 0, 1 => 1, 2 => 3, 3 => 2, 4 => 6, 5 => 5, 6 => 7, _ => null
            },
            ScenarioId.Eliminate => strategicMode switch
            {
                0 => 0, 1 => 13, 2 => 14, 4 => 6, 5 => 5, 6 => 7, _ => null
            },
            ScenarioId.Siege => strategicMode switch
            {
                0 => 10, 1 => 0, 2 => 3, 3 => 11, 4 => 12, 6 => 7, _ => null
            },
            ScenarioId.BigMan => strategicMode switch
            {
                0 => 0, 1 => 13, 2 => 14, 3 => 3, _ => null
            },
            ScenarioId.Armageddon => strategicMode switch
            {
                0 => 0, 1 => 1, 2 => 3, 3 => 2, 4 => 6, 5 => 3, _ => null
            },
            _ => null
        };

        if (family is null)
            return new OriginalAiFamilySelection(currentFamily, CopiesProjectedGangValue: false);

        // Every mode-4 branch which assigns a family also copies selector 0x5a's
        // signed 16-bit projection into the planning record's +12 word.
        return new OriginalAiFamilySelection(family.Value, CopiesProjectedGangValue: strategicMode == 4);
    }
}

internal readonly record struct OriginalAiFamilySelection(
    int Family,
    bool CopiesProjectedGangValue);
