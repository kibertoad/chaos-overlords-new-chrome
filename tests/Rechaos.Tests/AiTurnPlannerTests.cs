using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiTurnPlannerTests
{
    [Fact]
    public void PlannerIsDeterministicNonMutatingAndReturnsOnlyLegalCommands()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var before = MatchStateHasher.ComputeSha256(match);

        var first = AiTurnPlanner.Plan(match, new PlayerId(0));
        var second = AiTurnPlanner.Plan(match, new PlayerId(0));

        Assert.Equal(before, MatchStateHasher.ComputeSha256(match));
        Assert.Equal(first, second);
        Assert.Single(first);
        Assert.All(first, command => Assert.True(CommandValidator.Validate(match, command).IsValid));
        Assert.Equal(first.Count, first.Select(command => command.Gang).Distinct().Count());
    }

    [Fact]
    public void PlannerRejectsHumanAndInactiveTurns()
    {
        var computer = CreateMatch();
        Assert.Throws<InvalidOperationException>(() => AiTurnPlanner.Plan(computer, new PlayerId(0)));

        var human = CreateMatch(PlayerController.Human);
        human.FinishUpkeep();
        Assert.Throws<ArgumentException>(() => AiTurnPlanner.Plan(human, new PlayerId(0)));
    }

    [Fact]
    public void HirePlannerSelectsOnlyAnAffordableValidOfferWithoutMutation()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        var before = MatchStateHasher.ComputeSha256(match);

        var choice = AiTurnPlanner.ChooseHire(match, new PlayerId(0));

        Assert.Equal(before, MatchStateHasher.ComputeSha256(match));
        if (choice is not null)
            Assert.True(HireRules.Validate(match, new PlayerId(0),
                choice.GangDefinitionId, choice.SectorId).IsValid);
    }

    private static MatchState CreateMatch(PlayerController controller = PlayerController.Computer)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", controller),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20, [new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 10)]),
            new(setups[1], 20, [new MatchGangState(new GangId(20), new PlayerId(1), 2, 1, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], owner: id == 0 ? new PlayerId(0) : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 7, setups), players, sectors);
    }
}
