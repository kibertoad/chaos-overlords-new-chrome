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
        var definition = state.Definitions.Gangs.Single(item => item.Id == gang.DefinitionId);
        var result = EffectiveStatistics.From(definition.Stats);
        foreach (var itemId in EquippedItems(gang))
            result = result.Add(state.Definitions.Items[itemId].Stats);
        foreach (var site in state.Sectors[gang.SectorId].Sites.Where(site => site.InfluencedBy == gang.Owner))
            result = result.Add(state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Stats);
        return result;
    }

    private static IEnumerable<short> EquippedItems(MatchGangState gang)
    {
        if (gang.WeaponItemId is { } weapon) yield return weapon;
        if (gang.ArmorItemId is { } armor) yield return armor;
        if (gang.MiscellaneousItemId is { } miscellaneous) yield return miscellaneous;
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
