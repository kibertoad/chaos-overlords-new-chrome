namespace Rechaos.Core.GameModel;

/// <summary>
/// Scenario/hire-role dispatch recovered from the original planner at 0x00432da0.
/// Match planning preparation applies this recovered family update. The three
/// bounded family-1 continuations, their equipment gate, and their mode-5
/// destinations are live, as are all complete family handlers selected by the
/// original dispatcher.
/// </summary>
internal static class OriginalAiFamilyRules
{
    /// <summary>
    /// RULE-AI-002: the family a gang new to its slot takes for the scenario and hire role, or none
    /// where the table cell is blank.
    /// </summary>
    public static int? FamilyFor(ScenarioId scenario, int hireRole) => scenario switch
        {
            ScenarioId.Greed => hireRole switch
            {
                0 or 1 => 0, 2 => 3, 3 => 2, 4 => 6, 6 => 7, _ => null
            },
            ScenarioId.Power => hireRole switch
            {
                0 => 0, 1 => 1, 2 => 3, 3 => 2, 4 => 6, 5 => 5, 6 => 7, _ => null
            },
            ScenarioId.Acceptance => hireRole switch
            {
                0 or 1 => 0, 2 => 5, 3 => 2, 4 => 6, 6 => 7, _ => null
            },
            ScenarioId.Dominance => hireRole switch
            {
                0 or 1 => 0, 2 => 5, 3 => 2, 4 => 6, 5 => 3, 6 => 7, _ => null
            },
            ScenarioId.KillEmAll or ScenarioId.Big40 => hireRole switch
            {
                0 => 0, 1 => 1, 2 => 3, 3 => 2, 4 => 6, 5 => 5, 6 => 7, _ => null
            },
            ScenarioId.Siege => hireRole switch
            {
                0 => 0, 1 => 13, 2 => 14, 4 => 6, 5 => 5, 6 => 7, _ => null
            },
            ScenarioId.Eliminate => hireRole switch
            {
                0 => 10, 1 => 0, 2 => 3, 3 => 11, 4 => 12, 6 => 7, _ => null
            },
            ScenarioId.BigMan => hireRole switch
            {
                0 => 0, 1 => 13, 2 => 14, 3 => 3, _ => null
            },
            ScenarioId.Armageddon => hireRole switch
            {
                0 => 0, 1 => 1, 2 => 3, 3 => 2, 4 => 6, 5 => 3, _ => null
            },
            _ => null
        };
}
