using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyFiveRulesTests
{
    [Theory]
    [InlineData(GangAction.None, true)]
    [InlineData(GangAction.Control, true)]
    [InlineData(GangAction.Equip, true)]
    [InlineData(GangAction.Heal, true)]
    [InlineData(GangAction.Influence, false)]
    public void SupportContinuationHasExactPreviousActionCases(
        GangAction previousAction,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyFiveRules.UsesSupportSiteContinuation(previousAction));

    [Theory]
    [InlineData(GangAction.Attack, true)]
    [InlineData(GangAction.Hide, true)]
    [InlineData(GangAction.Move, true)]
    [InlineData(GangAction.Snitch, false)]
    public void OpponentContinuationHasExactPreviousActionCases(
        GangAction previousAction,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyFiveRules.UsesOpponentContinuation(previousAction));

    [Theory]
    [InlineData(7, -3, true)]
    [InlineData(8, -3, false)]
    [InlineData(7, -4, false)]
    public void HealGateUsesRecoveredStrictBoundaries(
        int force,
        int effectiveHeal,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyFiveRules.ShouldHeal(force, effectiveHeal));

    [Theory]
    [InlineData(5, 3, 1, 8, 4, 2, true)]
    [InlineData(1, -3, 0, 10, 12, 5, false)]
    public void AttackGateUsesComparisonTargetQuarterStrengthFormula(
        int attackerForce,
        int attackerCombat,
        int attackerDefense,
        int targetForce,
        int targetCombat,
        int targetDefense,
        bool expected) =>
        Assert.Equal(expected, OriginalAiFamilyFiveRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            targetForce, targetCombat, targetDefense));

    [Theory]
    [InlineData(ScenarioId.Siege, 11)]
    [InlineData(ScenarioId.Acceptance, 2)]
    public void ThreeMovesTransitionToScenarioSpecificFamily(
        ScenarioId scenario,
        int expected) =>
        Assert.Equal(expected, OriginalAiFamilyFiveRules.ThreeMoveTransitionFamily(
            scenario, GangAction.Move, GangAction.Move, GangAction.Move));

    [Theory]
    [InlineData(ScenarioId.Greed, 3, true)]
    [InlineData(ScenarioId.Greed, 4, false)]
    [InlineData(ScenarioId.Acceptance, 3, false)]
    public void GreedTerminateOverrideUsesStrictFourTurnBoundary(
        ScenarioId scenario,
        int turnsRemaining,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyFiveRules.ShouldTerminateForGreed(
                scenario, turnsRemaining));

    [Fact]
    public void HighestSupportSiteUsesFirstStrictMaximumAndSkipsFinishedSites()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data);

        Assert.Equal(2,
            OriginalAiFamilyFiveRules.SelectHighestSupportUnfinishedSite(match, 0));

        match.Sectors[0].Sites[2].Resistance = 0;
        Assert.Equal(1,
            OriginalAiFamilyFiveRules.SelectHighestSupportUnfinishedSite(match, 0));
    }

    [Fact]
    public void SupportScoreSumsOnlyPositiveUnfinishedSites()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data);

        Assert.Equal(8,
            OriginalAiFamilyFiveRules.UnfinishedSupportScore(match, 0));

        match.Sectors[0].Sites[2].Resistance = 0;
        Assert.Equal(4,
            OriginalAiFamilyFiveRules.UnfinishedSupportScore(match, 0));
    }

    private static MatchState CreateMatch(OriginalData data)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, 4, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 11, 16),
                new MatchSiteState(2, 5, 15)
            ], owner: id == 0 ? setups[0].Id : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Acceptance, GameDuration.SixMonths, 41, setups), players, sectors);
    }
}
