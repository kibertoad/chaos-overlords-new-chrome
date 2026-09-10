namespace Rechaos.Core.GameModel;

/// <summary>
/// Objective routing shared by original AI families 13 and 14. Recovered from
/// selector 0x1f and the terminal blocks at 0x0040b87d through 0x004677df.
/// </summary>
internal static class OriginalAiObjectiveFamilyRules
{
    public const int ContestedAttackMinimumForce = 5;
    public const int ContestedAttackAttempts = 3;
    public const int FamilyFourteenHealForceLimit = 10;

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

    public static bool ShouldFamilyFourteenTerminalHeal(
        ScenarioId scenario,
        int currentSectorId,
        GangAction plannedAction,
        GangAction previousAction,
        int force,
        int effectiveHeal) =>
        (IsObjectiveSector(scenario, currentSectorId)
            || plannedAction == GangAction.Equip)
        && previousAction == GangAction.Control
        && force < FamilyFourteenHealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal;

    public static bool ShouldHealOwnedObjectiveWithoutVisibleOpponent(
        ScenarioId scenario,
        int currentSectorId,
        bool ownedByActingPlayer,
        bool hasVisibleOpponent,
        int force,
        int effectiveHeal) =>
        IsObjectiveSector(scenario, currentSectorId)
        && ownedByActingPlayer
        && !hasVisibleOpponent
        && force < FamilyFourteenHealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal;

    public static bool ShouldScanContestedObjectiveTargets(
        int turnsRemaining,
        int visibleOpponentWeight) =>
        Math.Abs(turnsRemaining) % 2 == 0 && visibleOpponentWeight > 0;

    public static bool AcceptContestedAttackRetry(
        int attackerForce,
        int attackerCombat,
        int attackerDefense,
        int targetForce,
        int targetCombat,
        int targetDefense) =>
        (targetForce + targetCombat) / 4 - attackerDefense
        <= attackerForce + attackerCombat - targetDefense;

    public static GangAction SelectContestedObjectiveResult(
        bool selectedTarget,
        int force,
        int effectiveHeal)
    {
        if (selectedTarget && force >= ContestedAttackMinimumForce)
            return GangAction.Attack;
        return force < FamilyFourteenHealForceLimit
            && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal
            ? GangAction.Heal
            : GangAction.Control;
    }
}
