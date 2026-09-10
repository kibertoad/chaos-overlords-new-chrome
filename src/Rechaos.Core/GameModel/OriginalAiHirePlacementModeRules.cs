namespace Rechaos.Core.GameModel;

/// <summary>
/// Pure implementation of the original outer planner's transient hire-
/// placement mode override. Encoded sectors are passed directly to the
/// destination selector by adding the original 64-sector offset.
/// </summary>
internal static class OriginalAiHirePlacementModeRules
{
    private const int MaximumHireRole = 6;

    public static int Select(
        ScenarioId scenario,
        int adjustedRole,
        int persistentAnchor,
        int firstVisibleHostileSectorId,
        int gangSlotZeroSectorId)
    {
        ValidateInputs(
            scenario,
            adjustedRole,
            persistentAnchor,
            firstVisibleHostileSectorId,
            gangSlotZeroSectorId);

        if (adjustedRole != 4) return persistentAnchor;

        if (scenario == ScenarioId.Siege)
            return checked(gangSlotZeroSectorId + AiPlanningState.SectorAnchorOffset);

        if (scenario is ScenarioId.Eliminate or ScenarioId.BigMan)
            return persistentAnchor;

        return checked(firstVisibleHostileSectorId + AiPlanningState.SectorAnchorOffset);
    }

    private static void ValidateInputs(
        ScenarioId scenario,
        int adjustedRole,
        int persistentAnchor,
        int firstVisibleHostileSectorId,
        int gangSlotZeroSectorId)
    {
        if (!Enum.IsDefined(scenario))
            throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        if (adjustedRole is < 0 or > MaximumHireRole)
            throw new ArgumentOutOfRangeException(nameof(adjustedRole));
        if (!IsEncodedAnchor(persistentAnchor))
            throw new ArgumentOutOfRangeException(nameof(persistentAnchor));
        if (!IsRawSectorOrInactive(firstVisibleHostileSectorId))
            throw new ArgumentOutOfRangeException(nameof(firstVisibleHostileSectorId));
        if (!IsRawSectorOrInactive(gangSlotZeroSectorId))
            throw new ArgumentOutOfRangeException(nameof(gangSlotZeroSectorId));
    }

    private static bool IsEncodedAnchor(int anchor) =>
        anchor == AiPlanningState.SectorAnchorOffset - 1
        || anchor == AiPlanningState.InactiveSectorAnchor
        || anchor is >= AiPlanningState.SectorAnchorOffset
            and < AiPlanningState.SectorAnchorOffset + MatchLimits.SectorCount;

    private static bool IsRawSectorOrInactive(int sectorId) =>
        sectorId == OriginalAiHirePlacementRules.InactiveGangSector
        || sectorId is >= 0 and < MatchLimits.SectorCount;
}
