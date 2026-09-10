namespace Rechaos.Core.GameModel;

/// <summary>
/// Equipment-choice rules recovered from selector 0x61 and family handler 11.
/// Kept separate from the provisional scalar planner until gang-family dispatch
/// and the original planning-record cooldowns are represented in MatchState.
/// </summary>
internal static class OriginalAiEquipmentRules
{
    private const int UnequippedWeaponBaselineItem = 24;

    public static int? SelectFamily11WeaponUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int availableCash)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        if (availableCash < 0) throw new ArgumentOutOfRangeException(nameof(availableCash));
        if (gang.Owner != player.Id || !player.Gangs.Contains(gang))
            throw new ArgumentException("Gang does not belong to the supplied player.", nameof(gang));
        if (state.FindPlayer(player.Id) != player)
            throw new ArgumentException("Player does not belong to the match.", nameof(player));
        if (state.Definitions.Items.Count <= UnequippedWeaponBaselineItem)
            throw new InvalidOperationException("Original AI weapon selection requires the 64-item table.");

        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var localTechLimit = SpecialSiteRules.ResearchTechLimit(state, gang);
        var blade = FirstEligibleClassItem(state, player, 1, localTechLimit, availableCash);
        var melee = FirstEligibleClassItem(state, player, 0, localTechLimit, availableCash);
        var ranged = FirstEligibleClassItem(state, player, 2, localTechLimit, availableCash);

        // The original nested >= comparisons give later entries priority on a tie.
        var preferredType = new[]
            {
                (Type: -1, Score: statistics.Strength + statistics.Fighting + statistics.MartialArts),
                (Type: 1, Score: state.Definitions.Items[blade].Stats.Combat
                    + statistics.Strength + statistics.Blade),
                (Type: 0, Score: state.Definitions.Items[melee].Stats.Combat
                    + statistics.Strength),
                (Type: 2, Score: state.Definitions.Items[ranged].Stats.Combat
                    + statistics.Range)
            }
            .Aggregate((best, candidate) => candidate.Score >= best.Score ? candidate : best)
            .Type;
        if (preferredType < 0) return null;

        var currentWeapon = gang.WeaponItemId is { } equipped
            ? checked((int)equipped)
            : UnequippedWeaponBaselineItem;
        var selected = currentWeapon;
        var gangTech = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).TechLevel;
        for (var index = 0; index < state.Definitions.Items.Count; index++)
        {
            var item = state.Definitions.Items[index];
            if (item.Type != preferredType
                || !player.ResearchedItems.Contains(checked((short)index))
                || item.TechLevel > gangTech
                || item.Stats.Combat <= state.Definitions.Items[selected].Stats.Combat
                || item.Cost > availableCash) continue;
            selected = index;
        }

        return selected == UnequippedWeaponBaselineItem || selected == gang.WeaponItemId
            ? null
            : selected;
    }

    public static int WeaponReplacementCooldown(int itemCost)
    {
        if (itemCost < 0) throw new ArgumentOutOfRangeException(nameof(itemCost));
        return checked(itemCost * 3);
    }

    public static bool CanReplaceWeapon(int cooldown, GangAction previousAction) =>
        cooldown <= 0 && previousAction != GangAction.Attack;

    private static int FirstEligibleClassItem(
        MatchState state,
        MatchPlayerState player,
        int type,
        int techLimit,
        int availableCash)
    {
        for (var index = 0; index < state.Definitions.Items.Count; index++)
        {
            var item = state.Definitions.Items[index];
            if (item.Type == type
                && item.TechLevel <= techLimit
                && player.ResearchedItems.Contains(checked((short)index))
                && item.Cost <= availableCash) return index;
        }

        // Selector 0x6d initializes its result to item zero.
        return 0;
    }
}
