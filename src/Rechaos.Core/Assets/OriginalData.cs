using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Rechaos.Core.Assets;

public sealed record OriginalData(
    IReadOnlyList<SiteDefinition> Sites,
    IReadOnlyList<GangDefinition> Gangs,
    IReadOnlyList<ItemDefinition> Items)
{
    /// <summary>Id lookups for one definition set, built the first time that set is queried.</summary>
    /// <remarks>
    /// Resolution, AI planning and board projection resolve definitions by id constantly, and a
    /// linear scan per lookup showed up on the update thread. The index cannot be an instance
    /// field: the copy constructor behind <c>with</c> would hand a derived set the original set's
    /// index, so <see cref="Site"/> would answer with pre-change definitions, and the record's
    /// value equality would start comparing cache objects. Keying a static table on the instance
    /// keeps the index tied to the exact set it was built from. The entries are weak, so a
    /// definition set the process stops using is still collectable.
    /// </remarks>
    private static readonly ConditionalWeakTable<OriginalData, DefinitionIndex> Indexes = [];

    private DefinitionIndex Index =>
        Indexes.GetValue(this, static definitions => new DefinitionIndex(definitions));

    /// <summary>The one site definition with this id; throws when none carries it.</summary>
    public SiteDefinition Site(short id) => Index.Sites.TryGetValue(id, out var definition)
        ? definition
        : throw new InvalidOperationException($"No site definition has id {id}.");

    /// <summary>The one gang definition with this id; throws when none carries it.</summary>
    public GangDefinition Gang(short id) => Index.Gangs.TryGetValue(id, out var definition)
        ? definition
        : throw new InvalidOperationException($"No gang definition has id {id}.");

    /// <summary>Both id lookups for a single definition set; ids are unique per the validator.</summary>
    private sealed class DefinitionIndex(OriginalData definitions)
    {
        public Dictionary<short, SiteDefinition> Sites { get; } =
            definitions.Sites.ToDictionary(definition => definition.Id);

        public Dictionary<short, GangDefinition> Gangs { get; } =
            definitions.Gangs.ToDictionary(definition => definition.Id);
    }
}

public sealed record SiteDefinition(
    string Name, short Id, short Resistance, short Support, short Frequency,
    short Tolerance, short Cash, Statistics Stats, short Special);

public sealed record GangDefinition(
    string Name, short Id, string Description, short Force, short Upkeep,
    short TechLevel, Statistics Stats);

public sealed record ItemDefinition(
    string Name, short Id, string Description, short Type, short ResearchDifficulty,
    short Cost, short TechLevel, Statistics Stats, short AttackAnimation,
    short HitAnimation, short Sound,
    [property: JsonPropertyName("Unknown")] short CombatPortraitFrame);

public sealed record Statistics(
    short Combat, short Defense, short Stealth, short Detect, short Chaos,
    short Control, short Heal, short Influence, short Research, short Strength,
    short Blade, short Range, short Fighting, short MartialArts);
