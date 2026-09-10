namespace Rechaos.Core.GameModel;

/// <summary>
/// Research-site, item-priority, and continuation rules recovered from
/// original AI family 7 at 0x00436c70.
/// </summary>
internal static class OriginalAiFamilySevenRules
{
    public const int HealForceLimit = 8;

    private static readonly int[] MiscellaneousResearchPriority =
        [44, 41, 42, 43, 46, 50, 49, 52];

    public static bool EndsAfterPreliminaryAction(GangAction action) =>
        action is GangAction.Equip or GangAction.Move
            or GangAction.Attack or GangAction.Influence;

    public static bool ClearsPreviousTarget(GangAction previousAction) =>
        previousAction is GangAction.Equip or GangAction.Move
            or GangAction.Attack or GangAction.Influence;

    public static bool ShouldHeal(int force, int effectiveHeal) =>
        force < HealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal;

    public static bool CanAttackSelectedTarget(
        int attackerForce,
        int attackerCombat,
        int attackerDefense,
        int comparisonTargetForce,
        int comparisonTargetCombat,
        int comparisonTargetDefense) =>
        OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            comparisonTargetForce, comparisonTargetCombat,
            comparisonTargetDefense);

    public static int SelectBestOwnedResearchSector(
        MatchState state,
        PlayerId player,
        int currentSectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)currentSectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(currentSectorId));

        var selected = currentSectorId;
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            if (state.Sectors[sectorId].Owner != player
                || ResearchScore(state, selected) >= ResearchScore(state, sectorId))
                continue;
            selected = sectorId;
        }
        return selected;
    }

    public static int ResearchScore(MatchState state, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return state.Sectors[sectorId].Sites.Sum(site => (int)state.Definitions.Sites
            .Single(definition => definition.Id == site.DefinitionId).Stats.Research);
    }

    public static int? SelectFirstUnfinishedResearchSite(
        MatchState state,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        foreach (var site in state.Sectors[sectorId].Sites.OrderBy(site => site.Slot))
        {
            var research = state.Definitions.Sites
                .Single(definition => definition.Id == site.DefinitionId).Stats.Research;
            if (research > 0 && site.Resistance > 0) return site.Slot;
        }
        return null;
    }

    public static int? SelectContinuationResearchItem(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int previousItemId)
    {
        ValidateGang(state, player, gang);
        if (IsPendingResearch(state, player, previousItemId)) return previousItemId;

        var previousType = previousItemId is >= 0
                && previousItemId < state.Definitions.Items.Count
            ? state.Definitions.Items[previousItemId].Type
            : (short)99;
        return previousType switch
        {
            2 => SelectFirstResearchItemOfType(state, player, gang, 1),
            1 => SelectFirstResearchItemOfType(state, player, gang, 3),
            3 => SelectMiscellaneousResearchItem(state, player, gang),
            _ => SelectFirstResearchItemOfType(state, player, gang, 2)
        };
    }

    public static int? SelectFallbackResearchItem(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang)
    {
        ValidateGang(state, player, gang);
        foreach (var type in new[] { 2, 1, 0, 3 })
            if (SelectFirstResearchItemOfType(state, player, gang, type) is { } item)
                return item;
        return SelectMiscellaneousResearchItem(state, player, gang);
    }

    public static int? SelectFirstResearchItemOfType(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int type)
    {
        ValidateGang(state, player, gang);
        var techLimit = SpecialSiteRules.ResearchTechLimit(state, gang);
        for (var itemId = 1;
             itemId < Math.Min(64, state.Definitions.Items.Count);
             itemId++)
        {
            var item = state.Definitions.Items[itemId];
            if (item.Type == type
                && item.TechLevel <= techLimit
                && IsPendingResearch(state, player, itemId))
                return itemId;
        }
        return null;
    }

    public static int? SelectMiscellaneousResearchItem(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang)
    {
        ValidateGang(state, player, gang);
        var techLimit = SpecialSiteRules.ResearchTechLimit(state, gang);
        foreach (var itemId in MiscellaneousResearchPriority)
            if (itemId < state.Definitions.Items.Count
                && state.Definitions.Items[itemId].TechLevel <= techLimit
                && IsPendingResearch(state, player, itemId))
                return itemId;
        return null;
    }

    public static bool ShouldTerminateForGreed(
        ScenarioId scenario,
        int turnsRemaining) =>
        OriginalAiFamilyTwelveRules.ShouldTerminateForGreed(
            scenario, turnsRemaining);

    private static bool IsPendingResearch(
        MatchState state,
        MatchPlayerState player,
        int itemId) =>
        itemId > 0
        && itemId < Math.Min(64, state.Definitions.Items.Count)
        && state.Definitions.Items[itemId].Type != 99
        && player.RemainingResearch(state.Definitions, checked((short)itemId)) > 0;

    private static void ValidateGang(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        if (gang.Owner != player.Id || !player.Gangs.Contains(gang))
            throw new ArgumentException("Gang does not belong to the supplied player.", nameof(gang));
    }
}
