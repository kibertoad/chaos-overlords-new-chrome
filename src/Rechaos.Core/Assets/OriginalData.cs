using System.Text.Json.Serialization;

namespace Rechaos.Core.Assets;

public sealed record OriginalData(
    IReadOnlyList<SiteDefinition> Sites,
    IReadOnlyList<GangDefinition> Gangs,
    IReadOnlyList<ItemDefinition> Items)
{
    private readonly Lazy<IReadOnlyDictionary<short, SiteDefinition>> _sitesById =
        new(() => Sites.ToDictionary(definition => definition.Id));
    private readonly Lazy<IReadOnlyDictionary<short, GangDefinition>> _gangsById =
        new(() => Gangs.ToDictionary(definition => definition.Id));

    /// <summary>The one site definition with this id; throws when none or several carry it.</summary>
    public SiteDefinition Site(short id) => _sitesById.Value.TryGetValue(id, out var definition)
        ? definition
        : throw new InvalidOperationException($"No site definition has id {id}.");

    /// <summary>The one gang definition with this id; throws when none or several carry it.</summary>
    public GangDefinition Gang(short id) => _gangsById.Value.TryGetValue(id, out var definition)
        ? definition
        : throw new InvalidOperationException($"No gang definition has id {id}.");
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
