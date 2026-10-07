using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiObjectiveFamilyRulesTests
{
    [Theory]
    [InlineData(ScenarioId.BigMan, 13, 12)]
    [InlineData(ScenarioId.Siege, 13, 13)]
    [InlineData(ScenarioId.BigMan, 14, 14)]
    [InlineData(ScenarioId.Siege, 14, 15)]
    public void FamiliesMapToTheirExactObjectiveModes(
        ScenarioId scenario,
        int family,
        int expectedMode) =>
        Assert.Equal(expectedMode,
            OriginalAiObjectiveFamilyRules.SelectionMode(scenario, family));

    [Theory]
    [InlineData(ScenarioId.BigMan, 27, true)]
    [InlineData(ScenarioId.BigMan, 36, true)]
    [InlineData(ScenarioId.BigMan, 9, false)]
    [InlineData(ScenarioId.Siege, 9, true)]
    [InlineData(ScenarioId.Siege, 54, true)]
    [InlineData(ScenarioId.Siege, 27, false)]
    [InlineData(ScenarioId.Power, 27, false)]
    public void SelectorOneFRecognizesOnlyScenarioObjectives(
        ScenarioId scenario,
        int sector,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiObjectiveFamilyRules.IsObjectiveSector(scenario, sector));

    [Fact]
    public void OffObjectiveTerminalOverridePreservesOnlyEquip()
    {
        Assert.True(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.BigMan, 0, GangAction.Attack));
        Assert.True(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.Siege, 0, GangAction.None));
        Assert.False(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.BigMan, 0, GangAction.Equip));
        Assert.False(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.BigMan, 27, GangAction.Attack));
    }

    [Theory]
    [InlineData(ScenarioId.BigMan, 27, GangAction.None, GangAction.Control, 9, -3, true)]
    [InlineData(ScenarioId.Siege, 9, GangAction.None, GangAction.Control, 9, -3, true)]
    [InlineData(ScenarioId.BigMan, 0, GangAction.Equip, GangAction.Control, 9, -3, true)]
    [InlineData(ScenarioId.BigMan, 0, GangAction.None, GangAction.Control, 9, -3, false)]
    [InlineData(ScenarioId.BigMan, 27, GangAction.None, GangAction.Attack, 9, -3, false)]
    [InlineData(ScenarioId.BigMan, 27, GangAction.None, GangAction.Control, 10, -3, false)]
    [InlineData(ScenarioId.BigMan, 27, GangAction.None, GangAction.Control, 9, -4, false)]
    public void FamilyFourteenTerminalHealPreservesExactBoundaries(
        ScenarioId scenario,
        int sector,
        GangAction plannedAction,
        GangAction previousAction,
        int force,
        int effectiveHeal,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiObjectiveFamilyRules.ShouldFamilyFourteenTerminalHeal(
                scenario, sector, plannedAction, previousAction, force, effectiveHeal));

    // RULE-AI-031 heal_ok: Force below 10 and effective Heal above -4.
    [Theory]
    [InlineData(9, -3, true)]
    [InlineData(10, -3, false)]
    [InlineData(9, -4, false)]
    public void HealTestPreservesStatBoundaries(int force, int effectiveHeal, bool expected) =>
        Assert.Equal(expected, OriginalAiObjectiveFamilyRules.CanHeal(force, effectiveHeal));

    [Theory]
    [InlineData(26, 1, true)]
    [InlineData(26, 10, true)]
    [InlineData(25, 10, false)]
    [InlineData(26, 0, false)]
    public void ContestedObjectiveScanUsesRemainingTurnParityAndVisibility(
        int turnsRemaining,
        int visibleWeight,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiObjectiveFamilyRules.ShouldScanContestedObjectiveTargets(
                turnsRemaining, visibleWeight));

    [Theory]
    [InlineData(true, 5, -3, GangAction.Attack)]
    [InlineData(true, 4, -3, GangAction.Heal)]
    [InlineData(false, 9, -3, GangAction.Heal)]
    [InlineData(false, 10, -3, GangAction.None)]
    [InlineData(false, 9, -4, GangAction.None)]
    public void ContestedObjectiveResultPreservesAttackAndHealBoundaries(
        bool selectedTarget,
        int force,
        int effectiveHeal,
        GangAction expected) =>
        Assert.Equal(expected,
            OriginalAiObjectiveFamilyRules.SelectContestedObjectiveResult(
                selectedTarget, force,
                OriginalAiObjectiveFamilyRules.CanHeal(force, effectiveHeal)));

    [Fact]
    public void AttackRetryUsesQuarterTargetAttackAndInclusiveBoundary()
    {
        Assert.True(OriginalAiObjectiveFamilyRules.AcceptContestedAttackRetry(
            4, 2, 1, 8, 0, 5));
        Assert.False(OriginalAiObjectiveFamilyRules.AcceptContestedAttackRetry(
            4, 2, 1, 8, 0, 6));
    }

    // RULE-AI-031, FND-AI-063: the unset threshold always holds 0, so a site needs positive
    // Support to be chosen, and the first of two equal maxima wins.
    [Fact]
    public void SupportScanStartsFromTheZeroTheStackSlotHolds()
    {
        var data = BundledOriginalData.Load();
        var none = data.Sites.First(site => site.Support <= 0).Id;
        var positive = data.Sites.First(site => site.Support > 0).Id;

        Assert.Null(OriginalAiObjectiveFamilyRules.SelectHighestSupportUnfinishedSite(
            CreateMatch(data, none, none, none), 0));
        Assert.Equal(1, OriginalAiObjectiveFamilyRules.SelectHighestSupportUnfinishedSite(
            CreateMatch(data, none, positive, positive), 0));
    }

    private static MatchState CreateMatch(OriginalData data, short site0, short site1, short site2)
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
                new MatchSiteState(0, site0, 7),
                new MatchSiteState(1, site1, 16),
                new MatchSiteState(2, site2, 15)
            ], owner: id == 0 ? setups[0].Id : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.BigMan, GameDuration.SixMonths, 41, setups, MatchDeviations.Original), players, sectors);
    }
}
