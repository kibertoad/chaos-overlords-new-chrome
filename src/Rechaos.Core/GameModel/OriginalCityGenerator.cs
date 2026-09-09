using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// Clean-room implementation of the original executable's new-city and
/// headquarters placement routines for the supported version 1.1 data set.
/// </summary>
public static class OriginalCityGenerator
{
    private const int DensitySize = 32;
    private const int DensityCenters = 40;
    private const int GeneratedSiteCount = 21;
    private const int MaximumCombinedSiteModifier = 6;

    public static IReadOnlyList<int> HeadquartersCandidates { get; } = [9, 12, 30, 33, 51, 54];

    public static MatchSectorState[] Generate(
        OriginalData definitions,
        ScenarioId scenario,
        DeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(random);
        ValidateDefinitions(definitions);

        var density = new int[DensitySize, DensitySize];
        for (var center = 0; center < DensityCenters; center++)
        {
            var centerX = random.NextInt(DensitySize);
            var centerY = random.NextInt(DensitySize);
            for (var radius = 0; radius < 4; radius++)
            {
                for (var x = centerX - radius; x < centerX + radius; x++)
                {
                    for (var y = centerY - radius; y < centerY + radius; y++)
                    {
                        if (x is < 0 or >= DensitySize || y is < 0 or >= DensitySize) continue;
                        density[x, y] = Math.Min(4, density[x, y] + 1);
                    }
                }
            }
        }

        var sectors = new MatchSectorState[MatchLimits.SectorCount];
        for (var sectorId = 0; sectorId < sectors.Length; sectorId++)
        {
            var sectorX = sectorId % 8;
            var sectorY = sectorId / 8;
            var densitySum = 0;
            for (var x = sectorX * 4; x < sectorX * 4 + 4; x++)
                for (var y = sectorY * 4; y < sectorY * 4 + 4; y++)
                    densitySum += density[x, y];

            var income = ((densitySum * 10 / 16) + 5) / 10 + 3;
            var selected = new List<SiteDefinition>(MatchLimits.SitesPerSector)
            {
                DrawSite(definitions, scenario, random)
            };
            while (selected.Count < MatchLimits.SitesPerSector)
            {
                var candidate = DrawSite(definitions, scenario, random);
                if (selected.Any(site => site.Id == candidate.Id)) continue;
                selected.Add(candidate);
                if (!HasBalancedModifiers(selected)) selected.RemoveAt(selected.Count - 1);
            }

            sectors[sectorId] = new MatchSectorState(
                sectorId,
                selected.Select((site, slot) => new MatchSiteState(slot, site.Id, site.Resistance)).ToArray(),
                tolerance: 17 - income,
                income: income);
        }
        return sectors;
    }

    public static int[] AssignHeadquarters(MatchSectorState[] sectors, DeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(sectors);
        ArgumentNullException.ThrowIfNull(random);
        if (sectors.Length != MatchLimits.SectorCount
            || !sectors.Select(sector => sector.Id).SequenceEqual(Enumerable.Range(0, MatchLimits.SectorCount)))
            throw new ArgumentException("The city must contain sectors ordered from 0 through 63.", nameof(sectors));

        var permutation = new int[MatchLimits.PlayerCount];
        Array.Fill(permutation, -1);
        for (var player = 0; player < permutation.Length; player++)
        {
            int candidate;
            do candidate = random.NextInt(MatchLimits.PlayerCount);
            while (permutation.Contains(candidate));
            permutation[player] = candidate;
        }

        var assigned = permutation.Select(index => HeadquartersCandidates[index]).ToArray();
        foreach (var sectorId in assigned)
        {
            var sector = sectors[sectorId];
            var sites = sector.Sites.Select(site => new MatchSiteState(
                site.Slot,
                site.Slot == 0 ? MatchBootstrap.HeadquartersDefinitionId : site.DefinitionId,
                site.Slot == 0 ? 0 : site.Resistance,
                site.Slot == 0 ? null : site.InfluencedBy)).ToArray();
            sectors[sectorId] = new MatchSectorState(
                sector.Id, sites, tolerance: sector.Tolerance, chaos: sector.Chaos,
                crackdownActive: sector.CrackdownActive, isImportant: sector.IsImportant,
                income: sector.Income, crackdownTurnsRemaining: sector.CrackdownTurnsRemaining);
        }
        return assigned;
    }

    private static SiteDefinition DrawSite(
        OriginalData definitions,
        ScenarioId scenario,
        DeterministicRandom random)
    {
        while (true)
        {
            var id = checked((short)random.NextInt(GeneratedSiteCount));
            if (scenario == ScenarioId.Armageddon && id is 4 or 8) continue;
            return definitions.Sites.Single(site => site.Id == id);
        }
    }

    private static bool HasBalancedModifiers(IReadOnlyList<SiteDefinition> sites)
    {
        int[] sums = new int[14];
        foreach (var site in sites)
        {
            var stats = site.Stats;
            short[] values =
            [
                stats.Combat, stats.Defense, stats.Stealth, stats.Detect,
                stats.Chaos, stats.Control, stats.Heal, stats.Influence,
                stats.Research, stats.Strength, stats.Blade, stats.Range,
                stats.Fighting, stats.MartialArts
            ];
            for (var index = 0; index < sums.Length; index++) sums[index] += values[index];
        }
        return sums.All(sum => sum is >= -MaximumCombinedSiteModifier and <= MaximumCombinedSiteModifier);
    }

    private static void ValidateDefinitions(OriginalData definitions)
    {
        var siteIds = definitions.Sites.Select(site => site.Id).ToHashSet();
        if (!Enumerable.Range(0, GeneratedSiteCount).All(id => siteIds.Contains((short)id))
            || !siteIds.Contains(MatchBootstrap.HeadquartersDefinitionId))
            throw new ArgumentException("Original city generation requires site definitions 0 through 21.", nameof(definitions));
    }
}

/// <summary>Creates a playable match using the recovered standard new-game setup.</summary>
public static class OriginalMatchFactory
{
    public const int StandardStartingCash = 20;
    private const int OriginalPreludeRolls = MatchLimits.PlayerCount;

    public static MatchState Create(OriginalData definitions, MatchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(setup);
        var random = new DeterministicRandom(setup.InitialSeed);

        // Standard modes initialize one recovered 2..5 value per internal player
        // before constructing the city. The value is not yet used by our model,
        // but consuming it preserves the original random stream.
        for (var player = 0; player < OriginalPreludeRolls; player++) random.NextInclusive(4);

        var sectors = OriginalCityGenerator.Generate(definitions, setup.Scenario, random);
        var headquarters = OriginalCityGenerator.AssignHeadquarters(sectors, random);
        var starts = setup.Players.Select((player, index) => new MatchPlayerStart(
            player.Id, headquarters[index], ManualRules.MaximumForce,
            StandardStartingCash, Array.Empty<short>())).ToArray();
        var bootstrapped = MatchBootstrap.Create(definitions, setup, sectors, starts);
        return new MatchState(definitions, setup, bootstrapped.Players, bootstrapped.Sectors, random);
    }
}
