using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiStrategicStateTests
{
    [Fact]
    public void StandardInitializationConsumesSixOriginalRangeCalls()
    {
        var random = new DeterministicRandom(1996);

        var strategy = AiStrategicState.Initialize(Setup(AiDifficulty.Criminal), random);

        Assert.Equal(18, random.ConsumptionCount);
        for (var player = 0; player < MatchLimits.PlayerCount; player++)
        {
            Assert.InRange(strategy.Reaction(new PlayerId(player)), 3, 6);
            for (var other = 0; other < MatchLimits.PlayerCount; other++)
                Assert.Equal(0, strategy.Attitude(new PlayerId(player), new PlayerId(other)));
        }
    }

    [Fact]
    public void HomicidalInitializationConsumesNoReactionRandomnessAndTargetsHumans()
    {
        var random = new DeterministicRandom(1996);

        var strategy = AiStrategicState.Initialize(Setup(AiDifficulty.HomicidalManiac), random);

        Assert.Equal(0, random.ConsumptionCount);
        for (var observer = 0; observer < MatchLimits.PlayerCount; observer++)
        {
            Assert.Equal(AiStrategicState.MinimumAttitude,
                strategy.Attitude(new PlayerId(observer), new PlayerId(0)));
            Assert.Equal(AiStrategicState.MaximumAttitude,
                strategy.Attitude(new PlayerId(observer), new PlayerId(1)));
            Assert.Equal(0, strategy.Reaction(new PlayerId(observer)));
        }
    }

    [Fact]
    public void CombatAndControlReactionsAreDirectionalAndClampAtMinusTen()
    {
        int[] reactions = [3, 5, 3, 3, 3, 3];
        var strategy = AiStrategicState.Restore(reactions, new int[36]);

        strategy.RecordCombat(new PlayerId(0), new PlayerId(1), 3);
        Assert.Equal(-5, strategy.Attitude(new PlayerId(1), new PlayerId(0)));
        Assert.Equal(0, strategy.Attitude(new PlayerId(0), new PlayerId(1)));

        strategy.RecordControl(new PlayerId(1), new PlayerId(0));
        Assert.Equal(-10, strategy.Attitude(new PlayerId(1), new PlayerId(0)));
    }

    [Fact]
    public void RecoveryRunsWhenPlanningTransitionsIntoWholeTurnResolution()
    {
        var match = CreateOnePlayerMatch();
        Assert.Equal(0, match.AiStrategy.Attitude(new PlayerId(0), new PlayerId(1)));

        match.FinishUpkeep();
        Assert.Equal(0, match.AiStrategy.Attitude(new PlayerId(0), new PlayerId(1)));
        match.FinishCommand(new PlayerId(0));

        Assert.Equal(TurnPhase.Execution, match.Coordinator.Phase);
        Assert.Equal(1, match.AiStrategy.Attitude(new PlayerId(0), new PlayerId(1)));
    }

    [Fact]
    public void HomicidalAttitudesDoNotRecoverAtResolutionBoundary()
    {
        var match = CreateOnePlayerMatch(
            PlayerController.Human, AiDifficulty.HomicidalManiac);

        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));

        Assert.Equal(AiStrategicState.MinimumAttitude,
            match.AiStrategy.Attitude(new PlayerId(0), new PlayerId(0)));
    }

    [Fact]
    public void StrategicStateContributesToCanonicalHash()
    {
        var match = CreateOnePlayerMatch();
        var before = MatchStateHasher.ComputeSha256(match);

        match.AiStrategy.RecordCombat(new PlayerId(1), new PlayerId(0), 7);

        Assert.NotEqual(before, MatchStateHasher.ComputeSha256(match));
    }

    [Fact]
    public void PlannerAttacksOnlyVisibleOwnersWithNegativeAttitude()
    {
        var neutral = CreateAttackPlannerMatch(AiDifficulty.Criminal);
        var hostile = CreateAttackPlannerMatch(AiDifficulty.HomicidalManiac);
        neutral.FinishUpkeep();
        hostile.FinishUpkeep();

        Assert.DoesNotContain(AiTurnPlanner.Plan(neutral, new PlayerId(0)),
            command => command.Action == GangAction.Attack);
        Assert.Contains(AiTurnPlanner.Plan(hostile, new PlayerId(0)),
            command => command.Action == GangAction.Attack
                && command.Target == CommandTarget.Gang(new GangId(20)));
    }

    [Fact]
    public void SectorCombatAdvantageUsesStrictInteger75PercentBoundary()
    {
        var exactlyThreeOfFour = CreateTerritorialPressureMatch(
            AiDifficulty.Criminal, PlayerController.Human, advantagedSectors: 3);
        var allFour = CreateTerritorialPressureMatch(
            AiDifficulty.Criminal, PlayerController.Human, advantagedSectors: 4);
        exactlyThreeOfFour.FinishUpkeep();
        allFour.FinishUpkeep();

        exactlyThreeOfFour.PrepareAiPlanning(new PlayerId(0));
        allFour.PrepareAiPlanning(new PlayerId(0));

        Assert.Equal(0, exactlyThreeOfFour.AiStrategy.Attitude(new PlayerId(0), new PlayerId(1)));
        Assert.Equal(AiStrategicState.MinimumAttitude,
            allFour.AiStrategy.Attitude(new PlayerId(0), new PlayerId(1)));
    }

    [Theory]
    [InlineData(AiDifficulty.Goon, PlayerController.Human, false)]
    [InlineData(AiDifficulty.Goon, PlayerController.Computer, true)]
    [InlineData(AiDifficulty.Criminal, PlayerController.Human, true)]
    [InlineData(AiDifficulty.Criminal, PlayerController.Computer, true)]
    [InlineData(AiDifficulty.CrimeLord, PlayerController.Human, true)]
    [InlineData(AiDifficulty.CrimeLord, PlayerController.Computer, false)]
    [InlineData(AiDifficulty.HomicidalManiac, PlayerController.Computer, false)]
    public void SectorCombatAdvantageUsesOriginalMentalityAndTargetTypeGate(
        AiDifficulty difficulty,
        PlayerController targetController,
        bool expectedHostile)
    {
        var match = CreateTerritorialPressureMatch(difficulty, targetController, advantagedSectors: 4);
        match.FinishUpkeep();

        match.PrepareAiPlanning(new PlayerId(0));

        Assert.Equal(expectedHostile, match.AiStrategy.IsHostile(new PlayerId(0), new PlayerId(1)));
    }

    [Fact]
    public void PlanningPreparationRollsRoleAndAssignsOriginalFamily()
    {
        var match = CreateOnePlayerMatch();
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, 4);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(4, match.AiPlanning.PreviousHireRole(player));
        Assert.Equal(4, match.AiPlanning.CurrentHireRole(player));
        Assert.Equal(6, match.AiPlanning.Family(player, 0));
        Assert.Equal(AiPlanningState.UnusedFamily, match.AiPlanning.Family(player, 1));
    }

    [Fact]
    public void ComputerSubmissionsRecordOnlyAcceptedActionInStableGangSlot()
    {
        var match = CreateOnePlayerMatch();
        var player = new PlayerId(0);
        match.Players[0].AddGang(new MatchGangState(new GangId(7), player, 1, 0, 10));
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);

        var rejected = match.Submit(new GameCommand(
            player, new GangId(0), GangAction.Move, CommandTarget.Sector(18)));
        var accepted = match.Submit(new GameCommand(
            player, new GangId(7), GangAction.Hide, CommandTarget.None));

        Assert.False(rejected.Accepted);
        Assert.True(accepted.Accepted);
        Assert.Equal(GangAction.None, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(GangAction.Hide, match.AiPlanning.PlannedAction(player, 1));
    }

    [Fact]
    public void AcceptedComputerCancellationClearsPlannedAction()
    {
        var match = CreateOnePlayerMatch();
        var player = new PlayerId(0);
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);
        Assert.True(match.Submit(new GameCommand(
            player, new GangId(0), GangAction.Hide, CommandTarget.None)).Accepted);

        var cancelled = match.Cancel(player, new GangId(0));

        Assert.True(cancelled.Accepted);
        Assert.Equal(GangAction.None, match.AiPlanning.PlannedAction(player, 0));
    }

    private static MatchSetup Setup(AiDifficulty difficulty) => new(
        ScenarioId.Greed,
        GameDuration.SixMonths,
        1996,
        [
            new MatchPlayerSetup(new PlayerId(0), "HUMAN", PlayerController.Human),
            new MatchPlayerSetup(new PlayerId(1), "CPU", PlayerController.Computer)
        ],
        difficulty);

    private static MatchState CreateOnePlayerMatch(
        PlayerController controller = PlayerController.Computer,
        AiDifficulty difficulty = AiDifficulty.Criminal)
    {
        var definitions = BundledOriginalData.Load();
        var setup = new MatchSetup(
            ScenarioId.Greed,
            GameDuration.SixMonths,
            1996,
            [new MatchPlayerSetup(new PlayerId(0), "PLAYER", controller)],
            difficulty);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], income: 3))
            .ToArray();
        var player = new MatchPlayerState(setup.Players[0], 20,
            [new MatchGangState(new GangId(0), new PlayerId(0), 1, 0, 10)]);
        return new MatchState(definitions, setup, [player], sectors);
    }

    private static MatchState CreateAttackPlannerMatch(AiDifficulty difficulty)
    {
        var definitions = BundledOriginalData.Load();
        var detector = definitions.Gangs.OrderByDescending(gang => gang.Stats.Detect).First();
        var visible = definitions.Gangs.OrderBy(gang => gang.Stats.Stealth).First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "HUMAN", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), detector.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), new PlayerId(1), visible.Id, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? new PlayerId(0) : null, income: 3))
            .ToArray();
        return new MatchState(definitions, new MatchSetup(
            ScenarioId.KillEmAll, GameDuration.SixMonths, 1996, setups, difficulty), players, sectors);
    }

    private static MatchState CreateTerritorialPressureMatch(
        AiDifficulty difficulty,
        PlayerController targetController,
        int advantagedSectors)
    {
        var definitions = BundledOriginalData.Load();
        var strong = definitions.Gangs
            .Where(gang => gang.Stats.Detect >= 10)
            .OrderByDescending(gang => gang.Stats.Combat + gang.Stats.Defense)
            .First();
        var weak = definitions.Gangs
            .Where(gang => gang.Stats.Stealth <= strong.Stats.Detect
                && gang.Stats.Combat + gang.Stats.Defense >= 1)
            .OrderBy(gang => gang.Stats.Combat + gang.Stats.Defense)
            .First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "TARGET", targetController)
        ];
        var observerGangs = Enumerable.Range(0, advantagedSectors)
            .Select(id => new MatchGangState(new GangId(id), new PlayerId(0), strong.Id, id, 10))
            .ToArray();
        var targetGangs = Enumerable.Range(0, 4)
            .Select(id => new MatchGangState(new GangId(100 + id), new PlayerId(1), weak.Id, id, 10))
            .ToArray();
        MatchPlayerState[] players =
        [
            new(setups[0], 20, observerGangs),
            new(setups[1], 20, targetGangs)
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id < 4 ? new PlayerId(1) : null, income: 3))
            .ToArray();
        return new MatchState(definitions, new MatchSetup(
            ScenarioId.KillEmAll, GameDuration.SixMonths, 1996, setups, difficulty), players, sectors);
    }
}
