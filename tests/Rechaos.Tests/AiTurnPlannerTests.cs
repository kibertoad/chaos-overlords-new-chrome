using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
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
    public void PlannerDoesNotAttackUndetectableGang()
    {
        var data = BundledOriginalData.Load();
        var lowDetect = data.Gangs.OrderBy(gang => gang.Stats.Detect).First();
        var highStealth = data.Gangs.OrderByDescending(gang => gang.Stats.Stealth).First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), lowDetect.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), new PlayerId(1), highStealth.Id, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], income: 3))
            .ToArray();
        var match = new MatchState(data,
            new MatchSetup(ScenarioId.KillEmAll, GameDuration.SixMonths, 9, setups), players, sectors);
        match.FinishUpkeep();

        Assert.False(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
        Assert.Contains(CommandOptionCatalog.LegalCommands(match, new PlayerId(0), new GangId(10)),
            command => command.Action == GangAction.Attack);
        Assert.DoesNotContain(AiTurnPlanner.Plan(match, new PlayerId(0)),
            command => command.Action == GangAction.Attack);
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

    [Theory]
    [InlineData(ScenarioId.Greed)]
    [InlineData(ScenarioId.Power)]
    [InlineData(ScenarioId.Acceptance)]
    [InlineData(ScenarioId.Dominance)]
    public void AllComputerTimedMatchCompletesDeterministicallyAndReplays(ScenarioId scenario)
    {
        var first = DriveMatch(scenario, 1984);
        var second = DriveMatch(scenario, 1984);

        Assert.NotNull(first.State.Outcome);
        Assert.Equal(MatchEndReason.TimeLimit, first.State.Outcome!.Reason);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(second.State));
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, first);
        replay.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(replay, first.State.Definitions);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(replayed));
    }

    [Theory]
    [InlineData(ScenarioId.KillEmAll)]
    [InlineData(ScenarioId.Big40)]
    [InlineData(ScenarioId.Eliminate)]
    [InlineData(ScenarioId.Siege)]
    [InlineData(ScenarioId.BigMan)]
    [InlineData(ScenarioId.Armageddon)]
    public void AllComputerObjectiveMatchRunsDeterministicReplayWindow(ScenarioId scenario)
    {
        var first = DriveMatch(scenario, 2112, throughTurn: 20);
        var second = DriveMatch(scenario, 2112, throughTurn: 20);

        Assert.True(first.State.Outcome is not null || first.State.Coordinator.Turn > 20);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(second.State));
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, first);
        replay.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(replay, first.State.Definitions);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(replayed));
    }

    private static MatchReplayRecorder DriveMatch(ScenarioId scenario, int seed, int? throughTurn = null)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU ONE", PlayerController.Computer),
            new(new PlayerId(1), "CPU TWO", PlayerController.Computer)
        ];
        var recorder = new MatchReplayRecorder(OriginalMatchFactory.Create(
            data, new MatchSetup(scenario, GameDuration.SixMonths, seed, setups)));
        var boundaries = 0;
        while (recorder.State.Outcome is null
               && (throughTurn is null || recorder.State.Coordinator.Turn <= throughTurn)
               && boundaries++ < 1_000)
        {
            var state = recorder.State;
            switch (state.Coordinator.Phase)
            {
                case TurnPhase.Upkeep:
                    recorder.FinishUpkeep();
                    break;
                case TurnPhase.Command:
                    var commandPlayer = state.Coordinator.ActivePlayer!.Value;
                    foreach (var command in AiTurnPlanner.Plan(state, commandPlayer))
                        Assert.True(recorder.Submit(command).Accepted);
                    recorder.FinishCommand(commandPlayer);
                    break;
                case TurnPhase.Execution:
                    recorder.FinishExecutionPhase();
                    break;
                case TurnPhase.Hire:
                    var hiringPlayer = state.Coordinator.ActivePlayer!.Value;
                    if (AiTurnPlanner.ChooseHire(state, hiringPlayer) is { } hire)
                        Assert.True(recorder.QueueHire(
                            hiringPlayer, hire.GangDefinitionId, hire.SectorId).Accepted);
                    recorder.FinishHire(hiringPlayer);
                    break;
                case TurnPhase.PlayerElimination:
                    recorder.FinishPlayerElimination();
                    break;
            }
        }
        Assert.True(boundaries < 1_000, "AI match exceeded the phase-boundary safety limit.");
        return recorder;
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
