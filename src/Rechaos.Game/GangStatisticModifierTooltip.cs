using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Explains an effective gang statistic by listing every equipment and completed-site modifier
/// that moves it away from the gang definition's own value, and for Combat the weapon skills.
/// </summary>
/// <remarks>
/// The breakdown is what the next rebuild before planning will write (RULE-GANG-001), built from
/// the gang's items and sector as they stand. The panel shows the stored value, which resolution
/// reads until that rebuild; when an item, a move or a hire this turn separates the two, the
/// total is labelled as the next turn's and the stored value follows as the current one. When
/// the panel shows the definition's values instead, the breakdown starts from the value shown
/// and ends with the stored value labelled as the one in play.
/// </remarks>
public static class GangStatisticModifierTooltip
{
    // Keeps the widest breakdown line inside the hover panel even if a definition carries an
    // unusually long name.
    private const int NameColumns = 24;

    /// <param name="panelShowsBase">Whether the panel shows the definition's values.</param>
    public static IReadOnlyList<string> Lines(
        InformationEffect effect,
        MatchState state,
        MatchGangState gang,
        bool panelShowsBase = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var definition = state.Definitions.Gang(gang.DefinitionId);
        return Lines(effect, definition.Stats,
            EffectiveStatisticsCalculator.ModifiersForGang(state, gang),
            Value(effect, EffectiveStatisticsCalculator.ForGang(state, gang)),
            panelShowsBase);
    }

    /// <param name="current">
    /// The stored value, or null when it is the breakdown's own total.
    /// </param>
    /// <param name="panelShowsBase">
    /// Whether the panel shows the definition's values, so the stored value is not the one on
    /// screen and is named as the one in play.
    /// </param>
    public static IReadOnlyList<string> Lines(
        InformationEffect effect,
        Statistics baseStats,
        IReadOnlyList<GangStatisticsModifier> modifiers,
        int? current = null,
        bool panelShowsBase = false)
    {
        ArgumentNullException.ThrowIfNull(baseStats);
        ArgumentNullException.ThrowIfNull(modifiers);
        var applied = modifiers.Where(modifier => Value(effect, modifier.Stats) != 0).ToArray();
        var baseValue = Value(effect, baseStats);
        var total = baseValue + applied.Sum(modifier => Value(effect, modifier.Stats));
        var pending = current is { } stored && stored != total;
        if (applied.Length == 0 && !pending) return [];
        var lines = new List<string>(applied.Length + 3) { $"BASE {baseValue}" };
        foreach (var modifier in applied)
        {
            var value = Value(effect, modifier.Stats);
            var sign = value > 0 ? "+" : string.Empty;
            lines.Add($"{sign}{value} {Name(modifier.Name)} ({SourceLabel(modifier.Source)})");
        }
        if (pending) lines.Add($"NEXT TURN {total}");
        if (panelShowsBase) lines.Add($"IN PLAY {current ?? total}");
        else lines.Add(pending ? $"NOW {current}" : $"TOTAL {total}");
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
        // RULE-COMBAT-001: the skills that go with the weapon, or with bare hands, in Combat.
        GangModifierSource.WeaponSkills => "SKILLS",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private static int Value(InformationEffect effect, Statistics stats) =>
        Value(effect, EffectiveStatistics.From(stats));

    private static int Value(InformationEffect effect, EffectiveStatistics stats) => effect switch
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
