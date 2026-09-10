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
            Assert.InRange(strategy.Reaction(new PlayerId(player)), 2, 5);
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
        int[] reactions = [2, 5, 0, 0, 0, 0];
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

    private static MatchSetup Setup(AiDifficulty difficulty) => new(
        ScenarioId.Greed,
        GameDuration.SixMonths,
        1996,
        [
            new MatchPlayerSetup(new PlayerId(0), "HUMAN", PlayerController.Human),
            new MatchPlayerSetup(new PlayerId(1), "CPU", PlayerController.Computer)
        ],
        difficulty);

    private static MatchState CreateOnePlayerMatch()
    {
        var definitions = BundledOriginalData.Load();
        var setup = new MatchSetup(
            ScenarioId.Greed,
            GameDuration.SixMonths,
            1996,
            [new MatchPlayerSetup(new PlayerId(0), "CPU", PlayerController.Computer)]);
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
}
