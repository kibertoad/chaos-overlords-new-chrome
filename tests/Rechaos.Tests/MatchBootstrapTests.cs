using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class MatchBootstrapTests
{
    [Fact]
    public void CreatesRightHandsInEachControlledHeadquartersSector()
    {
        var (data, setup, sectors, starts) = Inputs(ScenarioId.Greed);

        var match = MatchBootstrap.Create(data, setup, sectors, starts);

        Assert.Equal(125, match.Players[0].Cash);
        Assert.Equal(250, match.Players[1].Cash);
        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.Equal(new PlayerId(1), match.Sectors[63].Owner);
        Assert.Null(sectors[0].Owner);
        Assert.Null(sectors[63].Owner);
        Assert.Collection(match.Players[0].Gangs, gang =>
        {
            Assert.Equal(new GangId(0), gang.Id);
            Assert.Equal(MatchBootstrap.RightHandsDefinitionId, gang.DefinitionId);
            Assert.Equal(0, gang.SectorId);
            Assert.Equal(10, gang.Force);
        });
        Assert.Collection(match.Players[1].Gangs, gang =>
        {
            Assert.Equal(new GangId(1), gang.Id);
            Assert.Equal(63, gang.SectorId);
            Assert.Equal(8, gang.Force);
        });
        Assert.Equal(
            ["BOOM BOXES", "COMBAT KNIFE", "COMBAT PISTOL", "COOL HATS", "LEATHERS", "METAL PIPE", "SHOCK PADS"],
            match.Players[0].ResearchedItems
                .Select(item => data.Items[item].Name)
                .Order(StringComparer.Ordinal));
        Assert.All(match.Players[0].ResearchedItems,
            item => Assert.Equal(0, match.Players[0].RemainingResearch(data, item)));
    }

    [Fact]
    public void ArmageddonStartsEveryPlayerAtFiveHundredWithAllRealItemsResearched()
    {
        var (data, setup, sectors, starts) = Inputs(ScenarioId.Armageddon);

        var match = MatchBootstrap.Create(data, setup, sectors, starts);

        var realItemCount = data.Items.Count(item => item.Type != 99);
        Assert.All(match.Players, player =>
        {
            Assert.Equal(MatchBootstrap.ArmageddonStartingCash, player.Cash);
            Assert.Equal(realItemCount, player.ResearchedItems.Count);
            Assert.All(player.ResearchedItems, item => Assert.Equal(0, player.RemainingResearch(data, item)));
        });
    }

    [Fact]
    public void FreshLocalSetupDetectsExactSmgMilkPlayerName()
    {
        var (data, _, sectors, starts) = Inputs(ScenarioId.Greed);
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "SMGMILK", PlayerController.Human),
            new(new PlayerId(1), "smgmilk", PlayerController.Computer)
        ];
        var setup = new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996, players);

        var match = MatchBootstrap.Create(data, setup, sectors, starts);

        Assert.True(match.Players[0].UsesMaximumHireForce);
        Assert.False(match.Players[1].UsesMaximumHireForce);
    }

    [Fact]
    public void RejectsAStartOutsideANeutralHeadquartersSector()
    {
        var (data, setup, sectors, starts) = Inputs(ScenarioId.Greed);
        sectors[0] = Sector(0, includeHeadquarters: false);

        Assert.Throws<ArgumentException>(() => MatchBootstrap.Create(data, setup, sectors, starts));
    }

    private static (OriginalData Data, MatchSetup Setup, MatchSectorState[] Sectors, MatchPlayerStart[] Starts)
        Inputs(ScenarioId scenario)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(scenario, GameDuration.SixMonths, 1996, players);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => Sector(id, includeHeadquarters: id is 0 or 63))
            .ToArray();
        MatchPlayerStart[] starts =
        [
            new(new PlayerId(0), 0, 10, 125, [1, 2, 3]),
            new(new PlayerId(1), 63, 8, 250, [4, 5, 6])
        ];
        return (data, setup, sectors, starts);
    }

    private static MatchSectorState Sector(int id, bool includeHeadquarters) => new(id,
    [
        new MatchSiteState(0, includeHeadquarters ? MatchBootstrap.HeadquartersDefinitionId : (short)0,
            includeHeadquarters ? 0 : 7),
        new MatchSiteState(1, 1, 5),
        new MatchSiteState(2, 2, 4)
    ]);
}
