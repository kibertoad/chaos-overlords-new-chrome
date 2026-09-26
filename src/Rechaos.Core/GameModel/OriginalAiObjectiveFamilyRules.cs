namespace Rechaos.Core.GameModel;

/// <summary>
/// Objective routing shared by original AI families 13 and 14. Recovered from
/// selector 0x1f and the terminal blocks at 0x0040b87d through 0x004677df.
/// </summary>
internal static class OriginalAiObjectiveFamilyRules
{
    public const int ContestedAttackMinimumForce = 5;
    /// <summary>FND-AI-062: both draw loops run while a counter set to 0 is below 5.</summary>
    public const int AttackDraws = 5;
    public const int ObjectiveEquipmentCooldown = 2;
    public const int FamilyFourteenHealForceLimit = 10;

    public static bool IsObjectiveSector(ScenarioId scenario, int sectorId)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return scenario switch
        {
            ScenarioId.BigMan => sectorId is 27 or 28 or 35 or 36,
            ScenarioId.Siege => OriginalCityGenerator.HeadquartersCandidates.Contains(sectorId),
            _ => false
        };
    }

    public static int SelectionMode(ScenarioId scenario, int family) =>
        (scenario, family) switch
        {
            (ScenarioId.BigMan, 13) => 12,
            (ScenarioId.Siege, 13) => 13,
            (ScenarioId.BigMan, 14) => 14,
            (ScenarioId.Siege, 14) => 15,
            (_, 13 or 14) => throw new ArgumentException(
                "Objective families are only dispatched by Big Man and Siege.",
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

    /// <summary>RULE-AI-031 heal_ok: Force below 10 and effective Heal above -4.</summary>
    public static bool CanHeal(int force, int effectiveHeal) =>
        force < FamilyFourteenHealForceLimit
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
        OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            targetForce, targetCombat, targetDefense);

    /// <summary>
    /// FND-AI-062: after the draws, Attack or Heal; a gang that fails the Heal test gets no write
    /// (None here), where FND-AI-039 read Control.
    /// </summary>
    public static GangAction SelectContestedObjectiveResult(
        bool selectedTarget,
        int force,
        bool healOk)
    {
        if (selectedTarget && force >= ContestedAttackMinimumForce)
            return GangAction.Attack;
        return healOk ? GangAction.Heal : GangAction.None;
    }

    public static int? SelectHighestSupportUnfinishedSite(
        MatchState state,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        // PLACEHOLDER: RULE-AI-031. The original never sets the starting threshold, so the scan
        // starts from a leftover stack value (BUG-AI-006); the rebuild starts from 0 until a run
        // of the original shows what the slot holds.
        var bestSupport = 0;
        int? bestSlot = null;
        foreach (var site in state.Sectors[sectorId].Sites.OrderBy(site => site.Slot))
        {
            var support = state.Definitions.Site(site.DefinitionId).Support;
            if (site.Resistance <= 0 || support <= bestSupport) continue;
            bestSupport = support;
            bestSlot = site.Slot;
        }
        return bestSlot;
    }
}
