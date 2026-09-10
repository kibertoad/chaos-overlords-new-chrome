namespace Rechaos.Core.GameModel;

/// <summary>
/// Objective routing shared by original AI families 13 and 14. Recovered from
/// selector 0x1f and the terminal blocks at 0x0040b87d and 0x004675c8.
/// </summary>
internal static class OriginalAiObjectiveFamilyRules
{
    public static bool IsObjectiveSector(ScenarioId scenario, int sectorId)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return scenario switch
        {
            ScenarioId.BigMan => sectorId is 27 or 28 or 35 or 36,
            ScenarioId.Eliminate => OriginalCityGenerator.HeadquartersCandidates.Contains(sectorId),
            _ => false
        };
    }

    public static int SelectionMode(ScenarioId scenario, int family) =>
        (scenario, family) switch
        {
            (ScenarioId.BigMan, 13) => 12,
            (ScenarioId.Eliminate, 13) => 13,
            (ScenarioId.BigMan, 14) => 14,
            (ScenarioId.Eliminate, 14) => 15,
            (_, 13 or 14) => throw new ArgumentException(
                "Objective families are only dispatched by Big Man and Eliminate.",
                nameof(scenario)),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };

    public static bool ShouldOverrideWithMove(
        ScenarioId scenario,
        int currentSectorId,
        GangAction plannedAction) =>
        !IsObjectiveSector(scenario, currentSectorId)
        && plannedAction != GangAction.Equip;
}
