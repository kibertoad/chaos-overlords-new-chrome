using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class BulkGangCommandsTests
{
    private const int HomeSector = 9;
    private const int NeighborSector = 10;
    private static readonly PlayerId Player = new(0);

    [Fact]
    public void OnlyTheAllowlistedOrdersMayBeGivenToAWholeSelection()
    {
        Assert.Equal(
        [
            GangAction.Attack, GangAction.Control, GangAction.Heal,
            GangAction.Hide, GangAction.Influence, GangAction.Move
        ], BulkGangCommands.ActionsFor(recurring: false));
        Assert.Equal(
            [GangAction.Control, GangAction.Heal, GangAction.Hide, GangAction.Influence],
            BulkGangCommands.ActionsFor(recurring: true));
        Assert.All(BulkGangCommands.ActionsFor(recurring: true),
            action => Assert.True(CommandRules.CanRepeat(action)));
        Assert.False(BulkGangCommands.Allows(GangAction.Equip));
        Assert.False(BulkGangCommands.Allows(GangAction.Terminate));
    }

    [Fact]
    public void OptionsListEveryReachableTargetOnceAndNothingOffTheAllowlist()
    {
        var match = CreateMatch(sectorOwner: Player);
        var gangs = Gangs(match);

        var options = BulkGangCommands.Options(match, Player, gangs, recurring: false);

        Assert.All(options, command => Assert.True(BulkGangCommands.Allows(command.Action)));
        Assert.Equal(
            options.Select(command => (command.Action, command.Target)).Distinct().Count(),
            options.Count);
        // Every gang stands in the same sector, so the sector's three sites are listed once each
        // however many of them could do the influencing.
        Assert.Equal(MatchLimits.SitesPerSector,
            options.Count(command => command.Action == GangAction.Influence));
        Assert.Contains(options, command => command.Action == GangAction.Move
            && command.Target == CommandTarget.Sector(NeighborSector));
    }

    [Fact]
    public void RecurringOptionsDropTheOrdersThatCannotRepeat()
    {
        var match = CreateMatch(sectorOwner: Player);

        var options = BulkGangCommands.Options(match, Player, Gangs(match), recurring: true);

        Assert.All(options, command => Assert.True(command.Repeat));
        Assert.DoesNotContain(options, command => command.Action == GangAction.Move);
        Assert.Contains(options, command => command.Action == GangAction.Influence);
    }

    [Fact]
    public void HealingOrdersTheHurtGangsAndLeavesTheHaleOnesAlone()
    {
        var match = CreateMatch(sectorOwner: Player);
        var player = match.FindPlayer(Player)!;
        player.Gangs[0].Force = ManualRules.MaximumForce;
        player.Gangs[1].Force = 4;
        player.Gangs[2].Force = ManualRules.MaximumForce;

        var plan = BulkGangCommands.Plan(match, Player, Gangs(match),
            new BulkCommandIntent(GangAction.Heal, CommandTarget.None, Repeat: true));

        Assert.Equal([player.Gangs[1].Id], plan.Commands.Select(command => command.Gang));
        Assert.Equal(2, plan.Skipped);
        Assert.Equal(3, plan.Total);
        Assert.All(plan.Commands, command => Assert.True(match.Submit(command).Accepted));
    }

    [Fact]
    public void AMoveOrdersOnlyAsManyGangsAsTheDestinationHasRoomFor()
    {
        // Four gangs already stand in the destination, so only two of the three picked can follow.
        var match = CreateMatch(sectorOwner: Player, gangsInNeighbor: 4);

        var plan = BulkGangCommands.Plan(match, Player, Gangs(match),
            new BulkCommandIntent(GangAction.Move, CommandTarget.Sector(NeighborSector), Repeat: false));

        Assert.Equal(2, plan.Commands.Count);
        Assert.Equal(1, plan.Skipped);
        // The picks are served in the order they were made.
        Assert.Equal([new GangId(10), new GangId(11)], plan.Commands.Select(command => command.Gang));
        Assert.All(plan.Commands, command => Assert.True(match.Submit(command).Accepted));
    }

    [Fact]
    public void AFullDestinationTakesNobody()
    {
        var match = CreateMatch(
            sectorOwner: Player, gangsInNeighbor: MatchLimits.FriendlyGangsPerSector);

        var plan = BulkGangCommands.Plan(match, Player, Gangs(match),
            new BulkCommandIntent(GangAction.Move, CommandTarget.Sector(NeighborSector), Repeat: false));

        Assert.True(plan.IsEmpty);
        Assert.Equal(3, plan.Skipped);
    }

    [Fact]
    public void ControlIsSkippedForEverybodyInASectorItsOverlordAlreadyHolds()
    {
        var owned = BulkGangCommands.Plan(CreateMatch(sectorOwner: Player), Player,
            [new GangId(10), new GangId(11), new GangId(12)],
            new BulkCommandIntent(GangAction.Control, CommandTarget.None, Repeat: true));
        var contested = BulkGangCommands.Plan(CreateMatch(sectorOwner: null), Player,
            [new GangId(10), new GangId(11), new GangId(12)],
            new BulkCommandIntent(GangAction.Control, CommandTarget.None, Repeat: true));

        Assert.True(owned.IsEmpty);
        Assert.Equal(3, contested.Commands.Count);
        Assert.Equal(0, contested.Skipped);
    }

    [Fact]
    public void AGangThatHasLeftTheSelectionsWorldIsSkippedRatherThanThrown()
    {
        var match = CreateMatch(sectorOwner: null);

        var plan = BulkGangCommands.Plan(match, Player, [new GangId(10), new GangId(999)],
            new BulkCommandIntent(GangAction.Control, CommandTarget.None, Repeat: true));

        Assert.Single(plan.Commands);
        Assert.Equal(1, plan.Skipped);
    }

    [Fact]
    public void AnOrderOffTheAllowlistIsNeverPlanned()
    {
        var match = CreateMatch(sectorOwner: Player);

        Assert.Throws<ArgumentOutOfRangeException>(() => BulkGangCommands.Plan(
            match, Player, Gangs(match),
            new BulkCommandIntent(GangAction.Terminate, CommandTarget.None, Repeat: false)));
    }

    [Fact]
    public void TheStatusLineSaysHowMuchOfTheSelectionTookTheOrderAndFitsTheConsole()
    {
        Assert.Equal("MOVE ORDERED FOR 4 GANGS", BulkGangCommands.Message(GangAction.Move, 4, 4));
        Assert.Equal("HEAL ORDERED FOR 2 OF 5", BulkGangCommands.Message(GangAction.Heal, 2, 5));
        Assert.Equal("NO GANG CAN INFLUENCE", BulkGangCommands.Rejection(GangAction.Influence));
        foreach (var action in BulkGangCommands.Actions)
        {
            Assert.True(CityStatusMessage.Fits(BulkGangCommands.Rejection(action)));
            for (var total = 1; total <= MatchLimits.FriendlyGangsPerSector; total++)
            for (var ordered = 1; ordered <= total; ordered++)
                Assert.True(CityStatusMessage.Fits(
                    BulkGangCommands.Message(action, ordered, total)));
        }
    }

    private static IReadOnlyList<GangId> Gangs(MatchState match) =>
        match.FindPlayer(Player)!.Gangs.Take(3).Select(gang => gang.Id).ToArray();

    private static MatchState CreateMatch(PlayerId? sectorOwner, int gangsInNeighbor = 0)
    {
        var data = BundledOriginalData.Load();
        var setupPlayer = new MatchPlayerSetup(Player, "ONE", PlayerController.Human);
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setupPlayer]);
        var definition = data.Gangs.OrderByDescending(gang => gang.TechLevel).First();
        var gangs = Enumerable.Range(0, 3)
            .Select(index => new MatchGangState(
                new GangId(10 + index), Player, definition.Id, HomeSector, 8))
            .Concat(Enumerable.Range(0, gangsInNeighbor)
                .Select(index => new MatchGangState(
                    new GangId(100 + index), Player, definition.Id, NeighborSector, 8)))
            .ToArray();
        var player = new MatchPlayerState(setupPlayer, 500, gangs);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, data.Sites[0].Resistance),
                new MatchSiteState(1, 1, data.Sites[1].Resistance),
                new MatchSiteState(2, 2, data.Sites[2].Resistance)
            ], owner: sectorOwner))
            .ToArray();
        var match = new MatchState(data, setup, [player], sectors);
        match.FinishUpkeep();
        return match;
    }
}
