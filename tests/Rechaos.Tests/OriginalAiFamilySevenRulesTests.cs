using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilySevenRulesTests
{
    [Theory]
    [InlineData(7, -3, true)]
    [InlineData(8, -3, false)]
    [InlineData(7, -4, false)]
    public void HealUsesRecoveredBoundaries(int force, int heal, bool expected)
    {
        Assert.Equal(expected, OriginalAiFamilySevenRules.ShouldHeal(force, heal));
    }

    [Fact]
    public void BestOwnedResearchSectorUsesFullSiteSumAndRetainsEarlierTie()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, sourceSites: [2, 0, 0],
            additionalOwnedSectors:
            [
                (1, new short[] { 4, 0, 0 }),
                (2, new short[] { 4, 0, 0 })
            ]);

        Assert.Equal(1, OriginalAiFamilySevenRules.ResearchScore(match, 0));
        Assert.Equal(5, OriginalAiFamilySevenRules.ResearchScore(match, 1));
        Assert.Equal(1, OriginalAiFamilySevenRules.SelectBestOwnedResearchSector(
            match, new PlayerId(0), 0));
    }

    [Fact]
    public void LocalSiteSelectionUsesFirstPositiveResearchUnfinishedSlot()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, sourceSites: [4, 2, 8],
            sourceResistance: [0, 3, 8]);

        Assert.Equal(1,
            OriginalAiFamilySevenRules.SelectFirstUnfinishedResearchSite(match, 0));
    }

    [Fact]
    public void ContinuationRepeatsPendingItemThenCyclesWeaponClasses()
    {
        var data = BundledOriginalData.Load();
        var pendingBlade = FirstPending(data, 1);
        var completedRanged = FirstPending(data, 2);
        var researched = RealItems(data).Where(item => item != pendingBlade).ToHashSet();
        researched.Add(checked((short)completedRanged));
        var match = CreateMatch(data, sourceSites: [4, 0, 0], researched: researched);
        var player = match.Players[0];
        var gang = player.Gangs[0];

        Assert.Equal(pendingBlade,
            OriginalAiFamilySevenRules.SelectContinuationResearchItem(
                match, player, gang, completedRanged));

        researched.Remove(checked((short)pendingBlade));
        match = CreateMatch(data, sourceSites: [4, 0, 0], researched: researched);
        player = match.Players[0];
        gang = player.Gangs[0];
        Assert.Equal(pendingBlade,
            OriginalAiFamilySevenRules.SelectContinuationResearchItem(
                match, player, gang, pendingBlade));
    }

    [Fact]
    public void FallbackUsesRangedThenBladeMeleeArmorAndFixedMiscPriority()
    {
        var data = BundledOriginalData.Load();
        var pendingMisc = new short[] { 41, 42, 43 };
        var researched = RealItems(data).Where(item => !pendingMisc.Contains(item)).ToHashSet();
        var match = CreateMatch(data, sourceSites: [4, 0, 0], researched: researched);

        Assert.Equal(41, OriginalAiFamilySevenRules.SelectFallbackResearchItem(
            match, match.Players[0], match.Players[0].Gangs[0]));
    }

    private static int FirstPending(OriginalData data, int type) => data.Items
        .Select((item, index) => (item, index))
        .First(value => value.index > 0 && value.item.Type == type
            && value.item.ResearchDifficulty > 0 && value.item.TechLevel <= 10)
        .index;

    private static IEnumerable<short> RealItems(OriginalData data) => data.Items
        .Select((item, index) => (item, index))
        .Where(value => value.item.Type != 99)
        .Select(value => checked((short)value.index));

    private static MatchState CreateMatch(
        OriginalData data,
        IReadOnlyList<short> sourceSites,
        IReadOnlyList<int>? sourceResistance = null,
        IReadOnlyList<(int Sector, short[] Sites)>? additionalOwnedSectors = null,
        IEnumerable<short>? researched = null)
    {
        var player = new PlayerId(0);
        MatchPlayerSetup[] setups =
        [
            new(player, "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), player, 27, 0, 10)],
                researchedItems: (researched ?? []).ToHashSet()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var owned = (additionalOwnedSectors ?? [])
            .ToDictionary(value => value.Sector, value => value.Sites);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id =>
            {
                var definitions = id == 0 ? sourceSites
                    : owned.TryGetValue(id, out var sites) ? sites
                    : new short[] { 0, 0, 0 };
                return new MatchSectorState(id,
                    Enumerable.Range(0, MatchLimits.SitesPerSector)
                        .Select(slot => new MatchSiteState(
                            slot, definitions[slot],
                            id == 0 && sourceResistance is not null
                                ? sourceResistance[slot]
                                : data.Sites.Single(site => site.Id == definitions[slot]).Resistance,
                            id == 0 && definitions[slot] == 4 ? player : null))
                        .ToArray(),
                    owner: id == 0 || owned.ContainsKey(id) ? player : null,
                    income: 0);
            })
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Siege, GameDuration.SixMonths, 41, setups), players, sectors);
    }
}
