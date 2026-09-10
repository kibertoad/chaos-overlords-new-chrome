using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// Equipment-choice rules recovered from selectors 0x61, 0x64, 0x6c, 0x74, and 0x75.
/// </summary>
internal static class OriginalAiEquipmentRules
{
    private const int UnequippedWeaponBaselineItem = 24;
    private const int UnequippedArmorBaselineItem = 0;
    private const int UnequippedMiscellaneousBaselineItem = 0;

    internal readonly record struct Upgrade(short ItemId, EquipmentSlot Slot);

    public static Upgrade? SelectFamilyOneUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot)
    {
        ValidateGang(state, player, gang);
        if ((uint)gangSlot >= MatchLimits.GangsPerPlayer
            || !ReferenceEquals(player.Gangs[gangSlot], gang))
            throw new ArgumentException("Gang slot does not identify the supplied gang.", nameof(gangSlot));
        if (!NeedsFamilyOneEquipment(state, player, gang)) return null;

        var weapon = SelectFamily11WeaponUpgrade(state, player, gang, player.Cash);
        if (state.AiPlanning.WeaponCooldown(player.Id, gangSlot) <= 0
            && weapon is { } weaponId)
            return new Upgrade(checked((short)weaponId), EquipmentSlot.Weapon);

        var armor = SelectArmorUpgrade(state, player, gang, player.Cash);
        if (state.AiPlanning.ArmorCooldown(player.Id, gangSlot) <= 0
            && armor is { } armorId)
            return new Upgrade(checked((short)armorId), EquipmentSlot.Armor);

        return null;
    }

    public static Upgrade? SelectObjectiveFamilyUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot)
    {
        ValidateGang(state, player, gang);
        if ((uint)gangSlot >= MatchLimits.GangsPerPlayer
            || !ReferenceEquals(player.Gangs[gangSlot], gang))
            throw new ArgumentException("Gang slot does not identify the supplied gang.", nameof(gangSlot));

        var previousAction = state.AiPlanning.PreviousAction(player.Id, gangSlot);
        var weapon = SelectFamily11WeaponUpgrade(state, player, gang, player.Cash);
        if (state.AiPlanning.WeaponCooldown(player.Id, gangSlot) <= 0
            && previousAction != GangAction.Attack
            && weapon is { } weaponId)
            return new Upgrade(checked((short)weaponId), EquipmentSlot.Weapon);

        var armor = SelectArmorUpgrade(state, player, gang, player.Cash);
        if (state.AiPlanning.ArmorCooldown(player.Id, gangSlot) <= 0
            && previousAction != GangAction.Attack
            && armor is { } armorId)
            return new Upgrade(checked((short)armorId), EquipmentSlot.Armor);

        var miscellaneous = SelectMiscellaneousChaosUpgrade(state, player, gang);
        return miscellaneous is { } miscellaneousId
            && state.Definitions.Items[miscellaneousId].Cost <= player.Cash
                ? new Upgrade(checked((short)miscellaneousId), EquipmentSlot.Miscellaneous)
                : null;
    }

    public static Upgrade? SelectFamilyElevenUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot)
    {
        ValidateGang(state, player, gang);
        if ((uint)gangSlot >= MatchLimits.GangsPerPlayer
            || !ReferenceEquals(player.Gangs[gangSlot], gang))
            throw new ArgumentException("Gang slot does not identify the supplied gang.", nameof(gangSlot));

        var previousAction = state.AiPlanning.PreviousAction(player.Id, gangSlot);
        if (previousAction == GangAction.Attack) return null;

        var weapon = SelectFamily11WeaponUpgrade(state, player, gang, player.Cash);
        if (state.AiPlanning.WeaponCooldown(player.Id, gangSlot) <= 0
            && weapon is { } weaponId)
            return new Upgrade(checked((short)weaponId), EquipmentSlot.Weapon);

        var armor = SelectArmorUpgrade(state, player, gang, player.Cash);
        if (state.AiPlanning.ArmorCooldown(player.Id, gangSlot) <= 0
            && armor is { } armorId)
            return new Upgrade(checked((short)armorId), EquipmentSlot.Armor);

        var miscellaneous = SelectMiscellaneousDetectUpgrade(state, player, gang);
        return miscellaneous is { } miscellaneousId
            && state.Definitions.Items[miscellaneousId].Cost <= player.Cash
                ? new Upgrade(checked((short)miscellaneousId), EquipmentSlot.Miscellaneous)
                : null;
    }

    public static int? SelectFamily11WeaponUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int availableCash)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        ValidateGang(state, player, gang);
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

    public static int? SelectArmorUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int availableCash)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        ValidateGang(state, player, gang);
        if (state.Definitions.Items.Count < 64)
            throw new InvalidOperationException("Original AI armor selection requires the 64-item table.");

        var selected = gang.ArmorItemId is { } equipped
            ? checked((int)equipped)
            : UnequippedArmorBaselineItem;
        var gangTech = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).TechLevel;
        for (var index = 0; index < 64; index++)
        {
            var item = state.Definitions.Items[index];
            if (item.Type != 3
                || !player.ResearchedItems.Contains(checked((short)index))
                || item.TechLevel > gangTech
                || item.Stats.Defense <= state.Definitions.Items[selected].Stats.Defense
                || item.Cost >= availableCash) continue;
            selected = index;
        }

        return selected == UnequippedArmorBaselineItem || selected == gang.ArmorItemId
            ? null
            : selected;
    }

    public static int? SelectMiscellaneousChaosUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang) =>
        SelectMiscellaneousUpgrade(state, player, gang, item => item.Stats.Chaos);

    public static int? SelectMiscellaneousDetectUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang) =>
        SelectMiscellaneousUpgrade(state, player, gang, item => item.Stats.Detect);

    private static int? SelectMiscellaneousUpgrade(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        Func<ItemDefinition, short> score)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        ArgumentNullException.ThrowIfNull(score);
        ValidateGang(state, player, gang);
        if (state.Definitions.Items.Count < 64)
            throw new InvalidOperationException("Original AI miscellaneous selection requires the 64-item table.");

        var selected = gang.MiscellaneousItemId is { } equipped
            ? checked((int)equipped)
            : UnequippedMiscellaneousBaselineItem;
        var gangTech = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).TechLevel;
        for (var index = 0; index < 64; index++)
        {
            var item = state.Definitions.Items[index];
            if (item.Type != 4
                || !player.ResearchedItems.Contains(checked((short)index))
                || item.TechLevel > gangTech
                || score(item) <= score(state.Definitions.Items[selected])) continue;
            selected = index;
        }

        return selected == UnequippedMiscellaneousBaselineItem
            || selected == gang.MiscellaneousItemId
                ? null
                : selected;
    }

    public static bool NeedsFamilyOneEquipment(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gang);
        ValidateGang(state, player, gang);

        var missingEquipment = gang.WeaponItemId is null || gang.ArmorItemId is null;
        var sourceColumn = gang.SectorId % 8;
        for (var vertical = -1; vertical <= 1; vertical++)
        {
            for (var horizontal = -1; horizontal <= 1; horizontal++)
            {
                if (sourceColumn == 0 && horizontal < 0
                    || sourceColumn == 7 && horizontal > 0) continue;
                var sectorId = gang.SectorId + vertical * 8 + horizontal;
                if (sectorId is < 0 or > MatchLimits.SectorCount) continue;

                var owner = sectorId == MatchLimits.SectorCount
                    ? 0
                    : state.Sectors[sectorId].Owner?.Value ?? -1;
                var hasVisibleHostileHuman = sectorId == MatchLimits.SectorCount
                    ? AliasedSectorWeightIsTen(state, player.Id)
                    : SectorWeightIsTen(state, player.Id, sectorId);
                var nearbyEquipmentNeed = state.Setup.Scenario == ScenarioId.Greed
                    ? hasVisibleHostileHuman && owner == player.Id.Value
                    : (owner >= 0 && owner != player.Id.Value) || hasVisibleHostileHuman;
                if (missingEquipment && nearbyEquipmentNeed) return true;
            }
        }

        return state.Sectors[gang.SectorId].Owner == player.Id
            && SectorWeightIsTen(state, player.Id, gang.SectorId);
    }

    public static int EquipmentReplacementCooldown(int itemCost)
    {
        if (itemCost < 0) throw new ArgumentOutOfRangeException(nameof(itemCost));
        return checked(itemCost * 3);
    }

    public static int WeaponReplacementCooldown(int itemCost) =>
        EquipmentReplacementCooldown(itemCost);

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

    private static bool SectorWeightIsTen(
        MatchState state,
        PlayerId observer,
        int sectorId) => state.Players.Any(player =>
            player.Id != observer
            && player.Status == PlayerStatus.Active
            && player.Setup.Controller == PlayerController.Human
            && state.AiStrategy.IsHostile(observer, player.Id)
            && player.Gangs.Any(gang => gang.IsActive
                && gang.SectorId == sectorId
                && state.CanPlayerDetectGang(observer, gang.Id)));

    private static bool AliasedSectorWeightIsTen(MatchState state, PlayerId player) =>
        player.Value + 1 < MatchLimits.PlayerCount
        && SectorWeightIsTen(state, new PlayerId(player.Value + 1), 0);

    private static void ValidateGang(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang)
    {
        if (gang.Owner != player.Id || !player.Gangs.Contains(gang))
            throw new ArgumentException("Gang does not belong to the supplied player.", nameof(gang));
        if (state.FindPlayer(player.Id) != player)
            throw new ArgumentException("Player does not belong to the match.", nameof(player));
    }
}
