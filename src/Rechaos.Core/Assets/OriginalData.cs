using System.Text.Json.Serialization;

namespace Rechaos.Core.Assets;

public sealed record OriginalData(
    IReadOnlyList<SiteDefinition> Sites,
    IReadOnlyList<GangDefinition> Gangs,
    IReadOnlyList<ItemDefinition> Items)
{
    /// <summary>The one site definition with this id; throws when none or several carry it.</summary>
    internal SiteDefinition Site(short id) => Sites.Single(definition => definition.Id == id);

    /// <summary>The one gang definition with this id; throws when none or several carry it.</summary>
    internal GangDefinition Gang(short id) => Gangs.Single(definition => definition.Id == id);
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
