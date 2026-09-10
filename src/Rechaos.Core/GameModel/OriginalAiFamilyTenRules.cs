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
        if (state.Definitions.Items.Count < 64)
            throw new InvalidOperationException(
                "Original family-10 armor selection requires the 64-item table.");

        var selected = gang.ArmorItemId is { } equipped
            ? checked((int)equipped)
            : UnequippedArmorBaselineItem;
        var gangTech = state.Definitions.Gangs
            .Single(definition => definition.Id == gang.DefinitionId).TechLevel;
        for (var index = 0; index < 64; index++)
        {
            var item = state.Definitions.Items[index];
            if (item.Type != 3
                || !player.ResearchedItems.Contains(checked((short)index))
                || item.TechLevel > gangTech
                || item.Stats.Defense <= state.Definitions.Items[selected].Stats.Defense)
                continue;
            selected = index;
        }

        return selected == UnequippedArmorBaselineItem
            || selected == gang.ArmorItemId
                ? null
                : selected;
    }

    public static bool CanEquipArmor(int cooldown, int itemCost, int cash)
    {
        if (itemCost < 0) throw new ArgumentOutOfRangeException(nameof(itemCost));
        if (cash < 0) throw new ArgumentOutOfRangeException(nameof(cash));
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

    public static bool ShouldMoveToStealthierSector(
        int currentCompletedStealth,
        int selectedCompletedStealth) =>
        currentCompletedStealth < selectedCompletedStealth;

    public static GangAction SelectStationaryAction(int priorChaosCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(priorChaosCount);
        return priorChaosCount == 0 ? GangAction.Chaos : GangAction.Hide;
    }

    public static int CompletedStealthScore(MatchState state, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        return state.Sectors[sectorId].Sites
            .Where(site => site.Resistance <= 0)
            .Sum(site => Math.Max(0, (int)state.Definitions.Sites
                .Single(definition => definition.Id == site.DefinitionId).Stats.Stealth));
    }
}
