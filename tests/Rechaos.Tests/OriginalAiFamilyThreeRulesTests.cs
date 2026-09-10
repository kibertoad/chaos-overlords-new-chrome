using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyThreeRulesTests
{
    [Theory]
    [InlineData(7, -3, true)]
    [InlineData(8, -3, false)]
    [InlineData(7, -4, false)]
    public void HealGatePreservesForceAndEffectiveHealBoundaries(
        int force,
        int effectiveHeal,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyThreeRules.ShouldHeal(force, effectiveHeal));

    [Theory]
    [InlineData(GangAction.None, true)]
    [InlineData(GangAction.Control, true)]
    [InlineData(GangAction.Equip, true)]
    [InlineData(GangAction.Heal, true)]
    [InlineData(GangAction.Influence, false)]
    [InlineData(GangAction.Snitch, false)]
    public void CashSiteContinuationHasExactPreviousActionCases(
        GangAction previousAction,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyThreeRules.UsesCashSiteContinuation(previousAction));

    [Theory]
    [InlineData(GangAction.Attack, true)]
    [InlineData(GangAction.Hide, true)]
    [InlineData(GangAction.Move, true)]
    [InlineData(GangAction.Influence, false)]
    public void OpponentContinuationHasExactPreviousActionCases(
        GangAction previousAction,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyThreeRules.UsesOpponentContinuation(previousAction));

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
        Assert.Equal(expected, OriginalAiFamilyThreeRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            targetForce, targetCombat, targetDefense));

    [Theory]
    [InlineData(ScenarioId.Siege, 11)]
    [InlineData(ScenarioId.Greed, 2)]
    public void ThreeMovesTransitionToScenarioSpecificFamily(
        ScenarioId scenario,
        int expected) =>
        Assert.Equal(expected, OriginalAiFamilyThreeRules.ThreeMoveTransitionFamily(
            scenario, GangAction.Move, GangAction.Move, GangAction.Move));

    [Fact]
    public void ThreeMoveTransitionRequiresAllThreeMoveActions()
    {
        Assert.Null(OriginalAiFamilyThreeRules.ThreeMoveTransitionFamily(
            ScenarioId.Siege, GangAction.Control, GangAction.Move, GangAction.Move));
        Assert.Null(OriginalAiFamilyThreeRules.ThreeMoveTransitionFamily(
            ScenarioId.Siege, GangAction.Move, GangAction.Heal, GangAction.Move));
        Assert.Null(OriginalAiFamilyThreeRules.ThreeMoveTransitionFamily(
            ScenarioId.Siege, GangAction.Move, GangAction.Move, GangAction.None));
    }

    [Theory]
    [InlineData(ScenarioId.Greed, 3, true)]
    [InlineData(ScenarioId.Greed, 4, false)]
    [InlineData(ScenarioId.Power, 3, false)]
    public void GreedTerminateOverrideUsesStrictFourTurnBoundary(
        ScenarioId scenario,
        int turnsRemaining,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyThreeRules.ShouldTerminateForGreed(
                scenario, turnsRemaining));

    [Fact]
    public void HighestCashSiteUsesFirstStrictMaximumAndSkipsFinishedSites()
    {
        var match = CreateSiteMatch();

        Assert.Equal(1,
            OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite(match, 0));

        match.Sectors[0].Sites[1].Resistance = 0;
        Assert.Equal(0,
            OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite(match, 0));
    }

    [Fact]
    public void SectorScoreSumsOnlyPositiveCashFromUnfinishedSites()
    {
        var match = CreateSiteMatch();
        match.Sectors[0].Sites[1].Resistance = 0;

        Assert.Equal(10, OriginalAiFamilyThreeRules.UnfinishedCashScore(match, 0));
    }

    private static MatchState CreateSiteMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, 4, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 3, 13),
                new MatchSiteState(1, 14, 14),
                new MatchSiteState(2, 3, 13)
            ], owner: id == 0 ? setups[0].Id : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 5, setups), players, sectors);
    }
}
