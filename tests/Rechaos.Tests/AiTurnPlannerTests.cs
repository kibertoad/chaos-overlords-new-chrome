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
    public void DifficultyChangesAggressionWithoutChangingRulesOrConsumingRandomness()
    {
        var goon = CreateMatch(difficulty: AiDifficulty.Goon);
        var crimeLord = CreateMatch(difficulty: AiDifficulty.CrimeLord);
        goon.FinishUpkeep();
        crimeLord.FinishUpkeep();
        var goonConsumption = goon.Random.ConsumptionCount;
        var crimeLordConsumption = crimeLord.Random.ConsumptionCount;

        var goonPlan = AiTurnPlanner.Plan(goon, new PlayerId(0));
        var crimeLordPlan = AiTurnPlanner.Plan(crimeLord, new PlayerId(0));

        Assert.True(AiTurnPlanner.DifficultyAttackBias(AiDifficulty.Goon)
            < AiTurnPlanner.DifficultyAttackBias(AiDifficulty.Criminal));
        Assert.True(AiTurnPlanner.DifficultyAttackBias(AiDifficulty.Criminal)
            < AiTurnPlanner.DifficultyAttackBias(AiDifficulty.CrimeLord));
        Assert.True(AiTurnPlanner.DifficultyAttackBias(AiDifficulty.CrimeLord)
            < AiTurnPlanner.DifficultyAttackBias(AiDifficulty.HomicidalManiac));
        Assert.All(goonPlan.Concat(crimeLordPlan), command =>
            Assert.True(CommandValidator.Validate(
                command.Player == new PlayerId(0) && goonPlan.Contains(command) ? goon : crimeLord,
                command).IsValid));
        Assert.Equal(goonConsumption, goon.Random.ConsumptionCount);
        Assert.Equal(crimeLordConsumption, crimeLord.Random.ConsumptionCount);
    }

    [Fact]
    public void PlannerRequiresStrictSoloControlAdvantageAtOriginalBoundary()
    {
        var data = BundledOriginalData.Load();
        var definition = data.Gangs.First(candidate =>
            ManualRules.MinimumSectorIncome - candidate.Stats.Control is >= 1 and < ManualRules.MaximumForce);
        var equalForce = ManualRules.MinimumSectorIncome - definition.Stats.Control;
        var equal = CreateNeutralControlMatch(data, definition.Id, equalForce);
        var advantage = CreateNeutralControlMatch(data, definition.Id, equalForce + 1);
        equal.FinishUpkeep();
        advantage.FinishUpkeep();

        var equalGang = equal.FindGang(new GangId(10))!;
        var advantageGang = advantage.FindGang(new GangId(10))!;
        Assert.False(AiTurnPlanner.CanSoloControl(equal, new PlayerId(0), equalGang));
        Assert.True(AiTurnPlanner.CanSoloControl(advantage, new PlayerId(0), advantageGang));
        Assert.DoesNotContain(AiTurnPlanner.Plan(equal, new PlayerId(0)),
            command => command.Action == GangAction.Control);
        Assert.Contains(AiTurnPlanner.Plan(advantage, new PlayerId(0)),
            command => command.Action == GangAction.Control);
    }

    [Fact]
    public void SoloControlEstimateDoesNotCountUndetectableDefenders()
    {
        var data = BundledOriginalData.Load();
        var attackerDefinition = data.Gangs.OrderBy(candidate => candidate.Stats.Detect).First();
        var defenderDefinition = data.Gangs.OrderByDescending(candidate =>
            candidate.Stats.Stealth + candidate.Stats.Control).First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), attackerDefinition.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), new PlayerId(1), defenderDefinition.Id, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], owner: id == 0 ? new PlayerId(1) : null,
                income: ManualRules.MinimumSectorIncome))
            .ToArray();
        var match = new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 23, setups), players, sectors);
        var attacker = match.FindGang(new GangId(10))!;

        Assert.False(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
        Assert.True(AiTurnPlanner.CanSoloControl(match, new PlayerId(0), attacker));
    }

    [Fact]
    public void PlannerUsesOriginalHealForceAndSkillBoundaries()
    {
        var data = BundledOriginalData.Load();
        var capable = data.Gangs.First(candidate => candidate.Stats.Heal >= -3).Id;
        var incapable = data.Gangs.First(candidate => candidate.Stats.Heal < -3).Id;
        var forceEight = CreateMatch(definitionId: capable, force: 8);
        var forceNine = CreateMatch(definitionId: capable, force: 9);
        var noHealSkill = CreateMatch(definitionId: incapable, force: 8);
        forceEight.FinishUpkeep();
        forceNine.FinishUpkeep();
        noHealSkill.FinishUpkeep();

        Assert.Contains(AiTurnPlanner.Plan(forceEight, new PlayerId(0)),
            command => command.Action == GangAction.Heal);
        Assert.DoesNotContain(AiTurnPlanner.Plan(forceNine, new PlayerId(0)),
            command => command.Action == GangAction.Heal);
        Assert.DoesNotContain(AiTurnPlanner.Plan(noHealSkill, new PlayerId(0)),
            command => command.Action == GangAction.Heal);
    }

    [Fact]
    public void EliminateMovementPrefersRecoveredHeadquartersCandidateSet()
    {
        var match = CreateMatch();
        const int ordinarySector = 10;

        foreach (var headquarters in OriginalCityGenerator.HeadquartersCandidates)
            Assert.True(
                AiTurnPlanner.DestinationValue(
                    match, new PlayerId(0), headquarters, ScenarioId.Eliminate)
                > AiTurnPlanner.DestinationValue(
                    match, new PlayerId(0), ordinarySector, ScenarioId.Eliminate));
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

    [Fact]
    public void HiringPreparationUsesZeroBasedOriginalTurnAndAdvancesCurrentRole()
    {
        var match = CreateMatch(scenario: ScenarioId.BigMan);
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, 6);
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);

        match.PrepareAiHiring(player);

        Assert.Equal(6, match.AiPlanning.PreviousHireRole(player));
        Assert.Equal(0, match.AiPlanning.CurrentHireRole(player));
    }

    [Fact]
    public void HiringPreparationLeavesRoleUnchangedWhenOriginalAttemptGateFails()
    {
        var match = CreateMatch(
            scenario: ScenarioId.Greed,
            ownsStartingSector: false);
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, 6);
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);

        match.PrepareAiHiring(player);

        Assert.Equal(6, match.AiPlanning.CurrentHireRole(player));
    }

    [Fact]
    public void HirePlannerUsesOriginalRoleRankingInsteadOfRecreationScalar()
    {
        var data = BundledOriginalData.Load();
        var affordable = data.Gangs.Where(gang => gang.Id != 0 && gang.Force <= 100).ToArray();
        var offers = affordable
            .SelectMany(first => affordable.Where(second => second.Id != first.Id)
                .Select(second => (first, second)))
            .SelectMany(pair => affordable.Where(third =>
                    third.Id != pair.first.Id && third.Id != pair.second.Id)
                .Select(third => new[] { pair.first, pair.second, third }))
            .First(candidate =>
            {
                var original = OriginalAiHireRules.SelectOfferIndex(
                    candidate, ScenarioId.Power, requestedMode: 0, availableCash: 100);
                var recreationScalar = candidate
                    .Select((gang, index) => (index,
                        score: gang.Force * 20 + gang.TechLevel * 10
                            - gang.Upkeep * 15 - HireRules.InitialCost(gang)))
                    .OrderByDescending(entry => entry.score)
                    .ThenBy(entry => candidate[entry.index].Id)
                    .First().index;
                return original.HasValue && original.Value != recreationScalar;
            });
        var match = CreateMatch(data: data, cash: 100,
            hirePool: offers.Select(gang => gang.Id).ToArray());
        match.FinishUpkeep();
        match.PrepareAiPlanning(new PlayerId(0));
        var expectedIndex = OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 0,
            availableCash: match.Players[0].Cash)!.Value;

        var choice = match.PrepareAiHiring(new PlayerId(0));

        Assert.NotNull(choice);
        Assert.Equal(offers[expectedIndex].Id, choice.GangDefinitionId);
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
                    recorder.PrepareAiPlanning(commandPlayer);
                    foreach (var command in AiTurnPlanner.Plan(state, commandPlayer))
                        Assert.True(recorder.Submit(command).Accepted);
                    if (recorder.PrepareAiHiring(commandPlayer) is { } planningHire)
                        Assert.True(recorder.QueueHire(commandPlayer,
                            planningHire.GangDefinitionId, planningHire.SectorId).Accepted);
                    recorder.FinishCommand(commandPlayer);
                    break;
                case TurnPhase.Execution:
                    recorder.FinishExecutionPhase();
                    break;
                case TurnPhase.Hire:
                    var hiringPlayer = state.Coordinator.ActivePlayer!.Value;
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

    private static MatchState CreateMatch(
        PlayerController controller = PlayerController.Computer,
        AiDifficulty difficulty = AiDifficulty.Criminal,
        short definitionId = 1,
        int force = 10,
        ScenarioId scenario = ScenarioId.Power,
        bool ownsStartingSector = true,
        OriginalData? data = null,
        int cash = 20,
        IReadOnlyList<short>? hirePool = null)
    {
        data ??= BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", controller),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], cash,
                [new MatchGangState(new GangId(10), new PlayerId(0), definitionId, 0, force)],
                hirePool),
            new(setups[1], 20, [new MatchGangState(new GangId(20), new PlayerId(1), 2, 1, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], owner: ownsStartingSector && id == 0 ? new PlayerId(0) : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 7, setups, difficulty), players, sectors);
    }

    private static MatchState CreateNeutralControlMatch(
        OriginalData data,
        short definitionId,
        int force)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), definitionId, 0, force)]),
            new(setups[1], 20, [])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], income: ManualRules.MinimumSectorIncome))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 17, setups), players, sectors);
    }
}
