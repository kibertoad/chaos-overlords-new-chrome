using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ScenarioCatalogTests
{
    [Fact]
    public void CatalogContainsFourTimedAndSixObjectiveScenarios()
    {
        Assert.Equal(10, ScenarioCatalog.All.Count);
        Assert.Equal(4, ScenarioCatalog.All.Count(scenario => scenario.IsTimed));
        Assert.Equal(6, ScenarioCatalog.All.Count(scenario => !scenario.IsTimed));
        Assert.Equal(Enum.GetValues<ScenarioId>(), ScenarioCatalog.All.Select(scenario => scenario.Id));
    }

    [Theory]
    [InlineData(GameDuration.SixMonths, 26)]
    [InlineData(GameDuration.OneYear, 52)]
    [InlineData(GameDuration.TwoYears, 104)]
    [InlineData(GameDuration.FourYears, 208)]
    public void TimeLimitsMatchManual(GameDuration duration, int turns) =>
        Assert.Equal(turns, ScenarioCatalog.Turns(duration));

    [Theory]
    [InlineData(GameDuration.SixMonths, 1, 10, 30)]
    [InlineData(GameDuration.OneYear, 1, 30, 100)]
    [InlineData(GameDuration.TwoYears, 1, 75, 250)]
    [InlineData(GameDuration.FourYears, 1, 300, 1000)]
    public void DominanceWeightsMatchManual(
        GameDuration duration, int cash, int support, int sector)
    {
        Assert.Equal(new DominanceWeights(cash, support, sector), ScenarioCatalog.Weights(duration));
    }

    [Fact]
    public void TimedScoringUsesDocumentedStatistic()
    {
        var state = new PlayerScoreState(Cash: 123, Support: 45, ControlledSectors: 6);
        Assert.Equal(123, ScenarioCatalog.TimedScore(ScenarioId.Greed, GameDuration.SixMonths, state));
        Assert.Equal(6, ScenarioCatalog.TimedScore(ScenarioId.Power, GameDuration.SixMonths, state));
        Assert.Equal(45, ScenarioCatalog.TimedScore(ScenarioId.Acceptance, GameDuration.SixMonths, state));
        Assert.Equal(75, ScenarioCatalog.TimedScore(ScenarioId.Dominance, GameDuration.SixMonths, state));
    }

    [Theory]
    [InlineData(ScenarioId.KillEmAll, 0, 0, 0, 0, true)]
    [InlineData(ScenarioId.Big40, 40, 0, 0, 0, true)]
    [InlineData(ScenarioId.Eliminate, 0, 0, 0, 0, true)]
    [InlineData(ScenarioId.Siege, 0, 6, 0, 1, true)]
    [InlineData(ScenarioId.BigMan, 0, 0, 40, 1, true)]
    [InlineData(ScenarioId.Armageddon, 64, 0, 0, 1, true)]
    public void ObjectiveThresholdsMatchManual(
        ScenarioId scenario,
        int sectors,
        int important,
        int bigManPoints,
        int opponentsAlive,
        bool expected)
    {
        var state = new PlayerScoreState(0, 0, sectors, true, opponentsAlive,
            OpposingRightHandsAlive: opponentsAlive, important, bigManPoints);
        Assert.Equal(expected, ScenarioCatalog.HasObjectiveVictory(scenario, state));
    }
}
