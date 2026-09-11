using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ManualRulesTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void FourThroughSixAreSuccesses(int roll, bool expected) =>
        Assert.Equal(expected, ManualRules.IsDieSuccess(roll));

    [Fact]
    public void SuccessCountingValidatesAndCountsDice() =>
        Assert.Equal(3, ManualRules.CountSuccesses([1, 4, 2, 5, 6]));

    [Theory]
    [InlineData(0, 3)]
    [InlineData(34, 37)]
    [InlineData(38, 40)]
    [InlineData(40, 40)]
    public void BribeAddsThreeAndCapsAtForty(int before, int after) =>
        Assert.Equal(after, ManualRules.ApplyBribe(before));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(40, 37)]
    public void SnitchSubtractsThreeAndFloorsAtZero(int before, int after) =>
        Assert.Equal(after, ManualRules.ApplySnitch(before));

    [Theory]
    [InlineData(-10, 100)]
    [InlineData(0, 100)]
    [InlineData(5, 100)]
    [InlineData(6, 95)]
    [InlineData(12, 65)]
    [InlineData(24, 5)]
    [InlineData(25, 0)]
    [InlineData(50, 0)]
    public void PoliceDetectionMatchesManualTable(int stealth, int percent) =>
        Assert.Equal(percent, ManualRules.PoliceDetectionPercent(stealth));

    [Fact]
    public void HiddenPoliceDetectionUsesDetectTwelveAgainstStealth() =>
        Assert.Equal(60, ManualRules.PoliceDetectionPercent(10, hidden: true));

    [Theory]
    [InlineData(1, 3, 4)]
    [InlineData(9, 5, 10)]
    [InlineData(10, 1, 10)]
    public void HealingCannotExceedTenForce(int current, int successes, int expected) =>
        Assert.Equal(expected, ManualRules.RestoreForce(current, successes));

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(-4, 0)]
    [InlineData(0, 4)]
    [InlineData(6, 10)]
    public void HealDicePoolIsBaseFourPlusSkillWithZeroFloor(int skill, int dice) =>
        Assert.Equal(dice, ManualRules.HealDiceCount(skill));

    [Fact]
    public void InfluencePoolAddsEveryParticipantsForceAndSkill() =>
        Assert.Equal(13, ManualRules.InfluenceDiceCount([(5, 2), (4, 2)]));

    [Theory]
    [InlineData(10, 3, 7)]
    [InlineData(2, 5, 0)]
    public void InfluenceProgressCannotDropBelowZero(int resistance, int successes, int expected) =>
        Assert.Equal(expected, ManualRules.ApplyInfluenceProgress(resistance, successes));

    [Fact]
    public void ChaosPoolAddsSectorIncomeOnceToAllParticipatingGangs() =>
        Assert.Equal(16, ManualRules.ChaosDiceCount([(5, 2), (4, 1)], 4));

    [Theory]
    [InlineData(5, true, 5)]
    [InlineData(5, false, 2)]
    [InlineData(0, false, 0)]
    public void ChaosIncomeDependsOnControlAndRoundsDown(int successes, bool controls, int expected) =>
        Assert.Equal(expected, ManualRules.ChaosIncome(successes, controls));

    [Theory]
    [InlineData(10, 10, false)]
    [InlineData(11, 10, true)]
    public void CrackdownRequiresChaosStrictlyAboveTolerance(int chaos, int tolerance, bool expected) =>
        Assert.Equal(expected, ManualRules.TriggersCrackdown(chaos, tolerance));

    [Fact]
    public void CombatRatingUsesCumulativeSkillsForWeaponClass()
    {
        var statistics = new EffectiveStatistics(
            Combat: 3, Defense: 0, Stealth: 0, Detect: 0, Chaos: 0, Control: 0,
            Heal: 0, Influence: 0, Research: 0, Strength: 2, Blade: 4, Range: 5,
            Fighting: 6, MartialArts: 7);

        Assert.Equal(18, ManualRules.CombatRating(statistics, null));
        Assert.Equal(5, ManualRules.CombatRating(statistics, 0));
        Assert.Equal(9, ManualRules.CombatRating(statistics, 1));
        Assert.Equal(8, ManualRules.CombatRating(statistics, 2));
    }

    [Theory]
    [InlineData(10, 5, 3, 12)]
    [InlineData(10, 5, 13, 2)]
    [InlineData(2, -1, 4, 0)]
    public void AttackPoolIncludesForceAndSubtractsDefense(
        int force, int combat, int defense, int expected) =>
        Assert.Equal(expected, ManualRules.AttackDiceCount(force, combat, defense));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(5, 2)]
    public void RetaliationDamageIsHalvedAndRoundedDown(int successes, int damage) =>
        Assert.Equal(damage, ManualRules.RetaliationDamage(successes));

    [Theory]
    [InlineData(10, 10, 50)]
    [InlineData(12, 10, 60)]
    [InlineData(4, 14, 0)]
    [InlineData(30, 10, 100)]
    public void HiddenAttackChanceIsFivePercentPerDetectDifference(
        int detect, int stealth, int expected) =>
        Assert.Equal(expected, ManualRules.HiddenAttackHitPercent(detect, stealth));

    [Fact]
    public void SectorDetectionUsesBestGangAndBandBonusFromEachHelper() =>
        Assert.Equal(24, ManualRules.SectorDetectionStrength([12, 19, 14, -3]));

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(25, 12)]
    public void EquipmentSalePaysHalfRoundedDown(int cost, int expected)
    {
        var item = new Rechaos.Core.Assets.ItemDefinition(
            "TEST", 0, "", 0, 0, (short)cost, 0,
            new Rechaos.Core.Assets.Statistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            0, 0, 0, 0);
        Assert.Equal(expected, EquipmentRules.SaleValue(item));
    }

    [Fact]
    public void ControlStrengthAddsForceAndSkillWithoutDice() =>
        Assert.Equal(13, ManualRules.ControlStrength([(5, 2), (4, 2)]));

    [Theory]
    [InlineData(12, 3, 0, 0, 9)]
    [InlineData(12, 3, 5, 2, 2)]
    [InlineData(4, 5, 3, 1, -5)]
    public void ControlMarginSubtractsIncomeDefenseAndSupport(
        int attack, int income, int defense, int support, int expected) =>
        Assert.Equal(expected, ManualRules.ControlMargin(attack, income, defense, support));
}
