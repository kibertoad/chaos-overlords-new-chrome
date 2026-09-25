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
    Site,

    /// <summary>The skills that go with the gang's weapon, added to Combat (RULE-COMBAT-001).</summary>
    WeaponSkills
}

/// <summary>One named contribution to a gang's effective statistics.</summary>
public readonly record struct GangStatisticsModifier(
    GangModifierSource Source,
    string Name,
    Statistics Stats);

/// <summary>
/// Builds a gang's fourteen statistics as RULE-GANG-001 does before every planning phase: the
/// definition's values, plus each item's, plus the completed sites of its sector when its player
/// owns that sector, and then the weapon skills in Combat (RULE-COMBAT-001).
/// </summary>
public static class EffectiveStatisticsCalculator
{
    /// <summary>
    /// The statistics the gang has now: the values stored at the last rebuild, which resolution
    /// reads, so an item bought or a site completed during a turn counts from the next rebuild.
    /// Every gang joins a match with stored values, so a gang without them is a construction bug.
    /// </summary>
    public static EffectiveStatistics ForGang(MatchState state, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        return gang.StoredStatistics ?? throw new InvalidOperationException(
            $"Gang {gang.Id.Value} has no stored statistics; every gang stores them when it joins a match.");
    }

    /// <summary>The values RULE-GANG-001 writes for the gang from the state as it stands.</summary>
    public static EffectiveStatistics Rebuilt(MatchState state, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        return Build(state, gang, modifiers: null);
    }

    /// <summary>
    /// Rebuilds and stores the statistics of every active gang, after the sectors' completed-site
    /// values are rebuilt and before planning (RULE-GANG-001). An inactive gang keeps its values.
    /// </summary>
    internal static void RebuildBeforePlanning(MatchState state)
    {
        foreach (var gang in state.Players.SelectMany(player => player.Gangs))
            if (gang.IsActive) gang.StoredStatistics = Rebuilt(state, gang);
    }

    /// <summary>
    /// Names every contribution behind <see cref="Rebuilt"/>, in the order it is applied, so the
    /// interface can explain where an effective statistic comes from. The last one is the weapon
    /// skills Combat takes, when they are not zero.
    /// </summary>
    public static IReadOnlyList<GangStatisticsModifier> ModifiersForGang(
        MatchState state,
        MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var modifiers = new List<GangStatisticsModifier>();
        Build(state, gang, modifiers);
        return modifiers;
    }

    /// <summary>
    /// The one RULE-GANG-001 aggregation: definition, then each source, then the weapon skills in
    /// Combat (RULE-COMBAT-001). The rebuild passes no list; the interface collects the
    /// contributions it names.
    /// </summary>
    private static EffectiveStatistics Build(
        MatchState state,
        MatchGangState gang,
        List<GangStatisticsModifier>? modifiers)
    {
        var result = EffectiveStatistics.From(state.Definitions.Gang(gang.DefinitionId).Stats);
        foreach (var modifier in SourceModifiers(state, gang))
        {
            result = result.Add(modifier.Stats);
            modifiers?.Add(modifier);
        }
        var skills = WeaponSkills(state, gang, result);
        if (skills != 0)
            modifiers?.Add(new GangStatisticsModifier(
                GangModifierSource.WeaponSkills, "WEAPON SKILLS",
                new Statistics(checked((short)skills), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
        return result with { Combat = checked(result.Combat + skills) };
    }

    private static IEnumerable<GangStatisticsModifier> SourceModifiers(
        MatchState state,
        MatchGangState gang)
    {
        foreach (var (source, itemId) in EquippedItems(gang))
        {
            var item = state.Definitions.Items[itemId];
            yield return new GangStatisticsModifier(source, item.Name, item.Stats);
        }
        foreach (var site in CompletedSitesOfOwnedSector(state, gang))
        {
            var definition = state.Definitions.Site(site.DefinitionId);
            yield return new GangStatisticsModifier(
                GangModifierSource.Site, definition.Name, definition.Stats);
        }
    }

    /// <summary>
    /// The skills Combat takes for the gang's weapon, read from the statistics with items and
    /// sites already added (RULE-COMBAT-001). A weapon of another type adds none.
    /// </summary>
    private static int WeaponSkills(MatchState state, MatchGangState gang, EffectiveStatistics statistics) =>
        ManualRules.WeaponSkills(statistics,
            gang.WeaponItemId is { } weapon ? state.Definitions.Items[weapon].Type : null);

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

    // RULE-GANG-001: a gang takes the completed sites of its sector only when its player owns the
    // sector, whoever completed them.
    private static IEnumerable<MatchSiteState> CompletedSitesOfOwnedSector(
        MatchState state,
        MatchGangState gang)
    {
        var sector = state.Sectors[gang.SectorId];
        if (sector.Owner != gang.Owner) return [];
        return sector.Sites.Where(site => SiteControlRules.Controller(sector, site) is not null);
    }
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

/// <summary>The fourteen statistics in record order, as the hash and the native save write them.</summary>
internal static class NativeStatistics
{
    /// <summary>Writes the fourteen values in record order without allocating.</summary>
    public static void Write(BinaryWriter writer, EffectiveStatistics value)
    {
        writer.Write(value.Combat); writer.Write(value.Defense); writer.Write(value.Stealth);
        writer.Write(value.Detect); writer.Write(value.Chaos); writer.Write(value.Control);
        writer.Write(value.Heal); writer.Write(value.Influence); writer.Write(value.Research);
        writer.Write(value.Strength); writer.Write(value.Blade); writer.Write(value.Range);
        writer.Write(value.Fighting); writer.Write(value.MartialArts);
    }

    public static int[] ToArray(EffectiveStatistics value) =>
    [
        value.Combat, value.Defense, value.Stealth, value.Detect, value.Chaos, value.Control,
        value.Heal, value.Influence, value.Research, value.Strength, value.Blade, value.Range,
        value.Fighting, value.MartialArts
    ];

    public static EffectiveStatistics FromArray(IReadOnlyList<int> values)
    {
        if (values.Count != 14)
            throw new InvalidDataException("A gang's statistics must hold fourteen values.");
        return new EffectiveStatistics(
            values[0], values[1], values[2], values[3], values[4], values[5], values[6],
            values[7], values[8], values[9], values[10], values[11], values[12], values[13]);
    }
}
