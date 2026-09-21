using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public readonly record struct EffectiveStatistics(
    int Combat,
    int Defense,
    int Stealth,
    int Detect,
    int Chaos,
    int Control,
    int Heal,
    int Influence,
    int Research,
    int Strength,
    int Blade,
    int Range,
    int Fighting,
    int MartialArts)
{
    public static EffectiveStatistics From(Statistics value) => new(
        value.Combat, value.Defense, value.Stealth, value.Detect,
        value.Chaos, value.Control, value.Heal, value.Influence,
        value.Research, value.Strength, value.Blade, value.Range,
        value.Fighting, value.MartialArts);

    public EffectiveStatistics Add(Statistics value) => new(
        Combat + value.Combat,
        Defense + value.Defense,
        Stealth + value.Stealth,
        Detect + value.Detect,
        Chaos + value.Chaos,
        Control + value.Control,
        Heal + value.Heal,
        Influence + value.Influence,
        Research + value.Research,
        Strength + value.Strength,
        Blade + value.Blade,
        Range + value.Range,
        Fighting + value.Fighting,
        MartialArts + value.MartialArts);
}

/// <summary>Where a modifier applied to a gang's statistics comes from.</summary>
public enum GangModifierSource
{
    Weapon,
    Armor,
    Miscellaneous,
    Site
}

/// <summary>One named contribution to a gang's effective statistics.</summary>
public readonly record struct GangStatisticsModifier(
    GangModifierSource Source,
    string Name,
    Statistics Stats);

/// <summary>
/// Calculates definition, equipped-item modifiers, and local influenced-site
/// modifiers for the influencing player's gangs.
/// </summary>
public static class EffectiveStatisticsCalculator
{
    public static EffectiveStatistics ForGang(MatchState state, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var definition = state.Definitions.Gang(gang.DefinitionId);
        var result = EffectiveStatistics.From(definition.Stats);
        foreach (var (_, itemId) in EquippedItems(gang))
            result = result.Add(state.Definitions.Items[itemId].Stats);
        foreach (var site in InfluencedSites(state, gang))
            result = result.Add(state.Definitions.Site(site.DefinitionId).Stats);
        return result;
    }

    /// <summary>
    /// Names every contribution behind <see cref="ForGang"/>, in the order it is applied, so the
    /// interface can explain where an effective statistic comes from. The resolution path stays on
    /// <see cref="ForGang"/>: this allocates the descriptions that only a reader needs.
    /// </summary>
    public static IReadOnlyList<GangStatisticsModifier> ModifiersForGang(
        MatchState state,
        MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var modifiers = new List<GangStatisticsModifier>();
        foreach (var (source, itemId) in EquippedItems(gang))
        {
            var item = state.Definitions.Items[itemId];
            modifiers.Add(new GangStatisticsModifier(source, item.Name, item.Stats));
        }
        foreach (var site in InfluencedSites(state, gang))
        {
            var definition = state.Definitions.Site(site.DefinitionId);
            modifiers.Add(new GangStatisticsModifier(
                GangModifierSource.Site, definition.Name, definition.Stats));
        }
        return modifiers;
    }

    private static IEnumerable<(GangModifierSource Source, short ItemId)> EquippedItems(
        MatchGangState gang)
    {
        if (gang.WeaponItemId is { } weapon)
            yield return (GangModifierSource.Weapon, weapon);
        if (gang.ArmorItemId is { } armor)
            yield return (GangModifierSource.Armor, armor);
        if (gang.MiscellaneousItemId is { } miscellaneous)
            yield return (GangModifierSource.Miscellaneous, miscellaneous);
    }

    private static IEnumerable<MatchSiteState> InfluencedSites(
        MatchState state,
        MatchGangState gang) =>
        state.Sectors[gang.SectorId].Sites.Where(site => site.InfluencedBy == gang.Owner);
}

public static class DiceRoller
{
    public static IReadOnlyList<int> RollD6(DeterministicRandom random, int count)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        var rolls = new int[count];
        for (var index = 0; index < rolls.Length; index++) rolls[index] = random.NextInt(6) + 1;
        return rolls;
    }
}
