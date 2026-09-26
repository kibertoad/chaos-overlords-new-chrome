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

    // RULE-OBJECTIVE-004: Kill 'Em All and Eliminate have no test of their own (FND-OBJECTIVE-003).
    [Theory]
    [InlineData(ScenarioId.KillEmAll, 0, 0, 0, 0, false)]
    [InlineData(ScenarioId.Big40, 40, 0, 0, 0, true)]
    [InlineData(ScenarioId.Eliminate, 0, 0, 0, 0, false)]
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

    // RULE-OBJECTIVE-004: the test runs for every slot, active or not.
    [Theory]
    [InlineData(ScenarioId.Big40, 40, 0, 0, 1)]
    [InlineData(ScenarioId.Siege, 0, 6, 0, 1)]
    [InlineData(ScenarioId.BigMan, 0, 0, 40, 1)]
    [InlineData(ScenarioId.Armageddon, 64, 0, 0, 1)]
    public void ObjectiveTestCountsEliminatedSlotsToo(
        ScenarioId scenario,
        int sectors,
        int important,
        int bigManPoints,
        int opponentsAlive)
    {
        var state = new PlayerScoreState(0, 0, sectors, false, opponentsAlive,
            OpposingRightHandsAlive: opponentsAlive, important, bigManPoints);

        Assert.True(ScenarioCatalog.HasObjectiveVictory(scenario, state));
    }

    [Theory]
    [InlineData(ScenarioId.KillEmAll, 0, 0, 0, 1)]
    [InlineData(ScenarioId.Big40, 39, 0, 0, 1)]
    [InlineData(ScenarioId.Eliminate, 0, 0, 0, 1)]
    [InlineData(ScenarioId.Siege, 0, 5, 0, 1)]
    [InlineData(ScenarioId.BigMan, 0, 0, 39, 1)]
    [InlineData(ScenarioId.Armageddon, 63, 0, 0, 1)]
    public void ObjectiveThresholdsRejectNearestIncompleteState(
        ScenarioId scenario,
        int sectors,
        int important,
        int bigManPoints,
        int opponentsAlive)
    {
        var state = new PlayerScoreState(0, 0, sectors, true, opponentsAlive,
            OpposingRightHandsAlive: opponentsAlive, important, bigManPoints);

        Assert.False(ScenarioCatalog.HasObjectiveVictory(scenario, state));
    }
}
