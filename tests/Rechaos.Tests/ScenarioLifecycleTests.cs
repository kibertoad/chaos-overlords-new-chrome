using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ScenarioLifecycleTests
{
    [Fact]
    public void BigManAwardsOnePointPerControlledCentralSectorBeforeVictoryCheck()
    {
        var match = CreateBigManMatch(initialPoints: 38);

        FinishTurn(match);

        Assert.Equal(40, match.Players[0].BigManPoints);
        var gameEvent = Assert.Single(match.Events, value => value.Kind == GameEventKind.BigManPointsAwarded);
        Assert.Equal(new BigManPointDetails(38, 2, 40), gameEvent.BigManPoints);
        Assert.Equal([new PlayerId(0)], match.Outcome!.Winners);
        Assert.True(gameEvent.Sequence < match.Events.Single(value => value.Kind == GameEventKind.MatchEnded).Sequence);
    }

    [Fact]
    public void EliminateRemovesPlayerWhenRightHandsDiesAndNeutralizesTheirState()
    {
        var match = CreateEliminateMatch();

        FinishTurnWithEliminatedPlayerRepeat(match);

        var eliminated = match.Players[1];
        Assert.Equal(PlayerStatus.Eliminated, eliminated.Status);
        Assert.All(eliminated.Gangs, gang =>
        {
            Assert.Equal(0, gang.Force);
            Assert.Null(gang.WeaponItemId);
            Assert.Null(gang.ArmorItemId);
            Assert.Null(gang.MiscellaneousItemId);
            Assert.Null(gang.QueuedCommand);
        });
        Assert.DoesNotContain(match.Commands.ExecutionPlan(), queued => queued.Command.Player == eliminated.Id);
        Assert.DoesNotContain(match.Sectors, sector => sector.Owner == eliminated.Id);
        Assert.DoesNotContain(match.Sectors.SelectMany(sector => sector.Sites),
            site => site.InfluencedBy == eliminated.Id);
        Assert.Equal([new PlayerId(0)], match.Outcome!.Winners);
        Assert.Contains(match.Events, value =>
            value.Kind == GameEventKind.PlayerEliminated && value.Player == eliminated.Id);
    }

    private static MatchState CreateBigManMatch(int initialPoints)
    {
        var data = BundledOriginalData.Load();
        var (setup, players) = CreatePlayers(
            ScenarioId.BigMan,
            new MatchPlayerStateFactory(BigManPoints: initialPoints),
            new MatchPlayerStateFactory());
        var sectors = CreateSectors(id => id is 27 or 28 ? new PlayerId(0) : null);
        return new MatchState(data, setup, players, sectors);
    }

    private static MatchState CreateEliminateMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Eliminate, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 500,
                [new MatchGangState(new GangId(10), new PlayerId(0), 0, 0, 10)]),
            new(setups[1], 500,
            [
                new MatchGangState(new GangId(20), new PlayerId(1), 0, 1, 0),
                new MatchGangState(new GangId(21), new PlayerId(1), 1, 1, 5,
                    weaponItemId: 0, armorItemId: 24, miscellaneousItemId: 38)
            ])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 0, id == 1 ? new PlayerId(1) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id is 1 or 2 ? new PlayerId(1) : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }

    private static (MatchSetup Setup, MatchPlayerState[] Players) CreatePlayers(
        ScenarioId scenario,
        params MatchPlayerStateFactory[] factories)
    {
        MatchPlayerSetup[] setups = factories.Select((_, id) =>
            new MatchPlayerSetup(new PlayerId(id), $"PLAYER {id + 1}", PlayerController.Human)).ToArray();
        var setup = new MatchSetup(scenario, GameDuration.SixMonths, 1996, setups);
        var players = setups.Select((player, id) => new MatchPlayerState(
            player, 500,
            [new MatchGangState(new GangId(id), player.Id, 0, id, 10)],
            bigManPoints: factories[id].BigManPoints)).ToArray();
        return (setup, players);
    }

    private static MatchSectorState[] CreateSectors(Func<int, PlayerId?> owner) =>
        Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner(id)))
            .ToArray();

    private static void FinishTurn(MatchState match)
    {
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
    }

    private static void FinishTurnWithEliminatedPlayerRepeat(MatchState match)
    {
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(21), GangAction.Hide, CommandTarget.None, Repeat: true)).Accepted);
        match.FinishCommand(new PlayerId(1));
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
    }

    private sealed record MatchPlayerStateFactory(int BigManPoints = 0);
}
