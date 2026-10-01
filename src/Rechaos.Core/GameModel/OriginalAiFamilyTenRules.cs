namespace Rechaos.Core.GameModel;

/// <summary>
/// Equipment, healing, Stealth-site movement, and concealment choices recovered
/// from original AI family 10 at 0x0042a6e0.
/// </summary>
internal static class OriginalAiFamilyTenRules
{
    public const int HealForceLimit = 10;
    public const int ArmorCooldown = 2;
    public const short SmokeBombItemId = 44;
    private const int UnequippedArmorBaselineItem = 1;

    /// <summary>
    /// Selector 0x72 (FND-AI-055): from the equipped armor, or item 1, the researched armor within
    /// the gang's Tech Level with the most Stealth, with no cost test.
    /// </summary>
    public static int? SelectArmorUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        if (state.FindPlayer(player.Id) != player
            || gang.Owner != player.Id
            || !player.Gangs.Contains(gang))
            throw new ArgumentException("Gang and player must belong to the match.");
        return OriginalAiEquipmentRules.SelectUpgradeOfType(
            state, player, gang,
            OriginalAiEquipmentRules.ArmorItemType, gang.ArmorItemId, UnequippedArmorBaselineItem,
            item => item.Stats.Stealth,
            _ => true);
    }

    /// <summary>
    /// FND-AI-071: the cost is compared with cash as signed values (<c>JG</c> at 0x0042A784), so a
    /// player whose upkeep left its cash below 0 (RULE-UPKEEP-001) fails the test.
    /// </summary>
    public static bool CanEquipArmor(int cooldown, int itemCost, int cash)
    {
        if (itemCost < 0) throw new ArgumentOutOfRangeException(nameof(itemCost));
        return cooldown <= 0 && itemCost <= cash;
    }

    public static bool ShouldEquipSmokeBombs(
        MatchPlayerState player,
        MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        return gang.MiscellaneousItemId is null
            && player.ResearchedItems.Contains(SmokeBombItemId);
    }

    public static bool ShouldHeal(
        int force,
        int effectiveHeal,
        bool hasVisibleOpponent) =>
        force < HealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal
        && !hasVisibleOpponent;

    /// <summary>
    /// RULE-AI-028, FND-AI-071: the handler moves when the selected sector's selector 8 value,
    /// <see cref="LastFinishedSiteStealth"/>, is strictly greater than the current sector's.
    /// </summary>
    public static bool ShouldMoveToStealthierSector(
        int currentLastFinishedStealth,
        int selectedLastFinishedStealth) =>
        currentLastFinishedStealth < selectedLastFinishedStealth;

    public static GangAction SelectStationaryAction(int priorChaosCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(priorChaosCount);
        return priorChaosCount == 0 ? GangAction.Chaos : GangAction.Hide;
    }

    /// <summary>
    /// RULE-AI-028, FND-AI-071: selector 8, which the handler compares for the current sector and
    /// the one sector selector mode 9 returns. It walks the three site slots and, for each
    /// finished site (selector 0x1C), replaces its result with that site's Stealth, so it gives
    /// the Stealth of the last finished site, whatever its sign, and 0 when none is finished.
    /// </summary>
    public static int LastFinishedSiteStealth(MatchState state, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        var stealth = 0;
        foreach (var site in state.Sectors[sectorId].Sites)
            if (site.Resistance <= 0)
                stealth = state.Definitions.Site(site.DefinitionId).Stats.Stealth;
        return stealth;
    }

    /// <summary>
    /// RULE-AI-006: sector selector mode 9's score, the positive Stealth of the sector's finished
    /// sites summed.
    /// </summary>
    public static int CompletedStealthScore(MatchState state, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        return state.Sectors[sectorId].Sites
            .Where(site => site.Resistance <= 0)
            .Sum(site => Math.Max(0, (int)state.Definitions.Site(site.DefinitionId).Stats.Stealth));
    }
}
