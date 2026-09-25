using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Explains an effective gang statistic by listing every equipment and completed-site modifier
/// that moves it away from the gang definition's own value, and for Combat the weapon skills.
/// </summary>
public static class GangStatisticModifierTooltip
{
    // Keeps the widest breakdown line inside the hover panel even if a definition carries an
    // unusually long name.
    private const int NameColumns = 24;

    public static IReadOnlyList<string> Lines(
        InformationEffect effect,
        MatchState state,
        MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var definition = state.Definitions.Gang(gang.DefinitionId);
        return Lines(effect, definition.Stats,
            EffectiveStatisticsCalculator.ModifiersForGang(state, gang));
    }

    public static IReadOnlyList<string> Lines(
        InformationEffect effect,
        Statistics baseStats,
        IReadOnlyList<GangStatisticsModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(baseStats);
        ArgumentNullException.ThrowIfNull(modifiers);
        var applied = modifiers.Where(modifier => Value(effect, modifier.Stats) != 0).ToArray();
        if (applied.Length == 0) return [];
        var baseValue = Value(effect, baseStats);
        var lines = new List<string>(applied.Length + 2) { $"BASE {baseValue}" };
        var total = baseValue;
        foreach (var modifier in applied)
        {
            var value = Value(effect, modifier.Stats);
            total += value;
            var sign = value > 0 ? "+" : string.Empty;
            lines.Add($"{sign}{value} {Name(modifier.Name)} ({SourceLabel(modifier.Source)})");
        }
        lines.Add($"TOTAL {total}");
        return lines;
    }

    private static string Name(string name)
    {
        var upper = name.ToUpperInvariant();
        return upper.Length <= NameColumns ? upper : upper[..NameColumns];
    }

    private static string SourceLabel(GangModifierSource source) => source switch
    {
        GangModifierSource.Weapon => "WEAPON",
        GangModifierSource.Armor => "ARMOR",
        GangModifierSource.Miscellaneous => "MISC",
        GangModifierSource.Site => "SITE",
        // RULE-COMBAT-001: the skills of the weapon are part of the stored Combat.
        GangModifierSource.WeaponSkills => "WEAPON",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private static int Value(InformationEffect effect, Statistics stats) => effect switch
    {
        InformationEffect.Combat => stats.Combat,
        InformationEffect.Defense => stats.Defense,
        InformationEffect.Chaos => stats.Chaos,
        InformationEffect.Control => stats.Control,
        InformationEffect.Heal => stats.Heal,
        InformationEffect.Influence => stats.Influence,
        InformationEffect.Research => stats.Research,
        InformationEffect.Stealth => stats.Stealth,
        InformationEffect.Detect => stats.Detect,
        InformationEffect.Strength => stats.Strength,
        InformationEffect.Blade => stats.Blade,
        InformationEffect.Range => stats.Range,
        InformationEffect.Fighting => stats.Fighting,
        InformationEffect.MartialArts => stats.MartialArts,
        _ => throw new ArgumentOutOfRangeException(nameof(effect))
    };
}
