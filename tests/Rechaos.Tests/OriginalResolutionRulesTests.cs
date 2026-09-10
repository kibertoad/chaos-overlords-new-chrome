using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalResolutionRulesTests
{
    [Fact]
    public void ComputerBandFollowsMentalityWhileHumansRemainStandard()
    {
        Assert.Equal(OriginalResolutionBand.Goon,
            OriginalResolutionRules.Band(PlayerController.Computer, AiDifficulty.Goon));
        Assert.Equal(OriginalResolutionBand.Standard,
            OriginalResolutionRules.Band(PlayerController.Computer, AiDifficulty.Criminal));
        Assert.Equal(OriginalResolutionBand.Expert,
            OriginalResolutionRules.Band(PlayerController.Computer, AiDifficulty.CrimeLord));
        Assert.Equal(OriginalResolutionBand.Expert,
            OriginalResolutionRules.Band(PlayerController.Computer, AiDifficulty.HomicidalManiac));
        Assert.Equal(OriginalResolutionBand.Standard,
            OriginalResolutionRules.Band(PlayerController.Human, AiDifficulty.HomicidalManiac));
    }

    [Fact]
    public void OnlySelectedGoonActionPoolsLoseOneFifth()
    {
        Assert.Equal(9, OriginalResolutionRules.ActionPool(
            OriginalResolutionBand.Goon, GangAction.Influence, 11));
        Assert.Equal(9, OriginalResolutionRules.ActionPool(
            OriginalResolutionBand.Goon, GangAction.Research, 11));
        Assert.Equal(9, OriginalResolutionRules.ActionPool(
            OriginalResolutionBand.Goon, GangAction.Chaos, 11));
        Assert.Equal(11, OriginalResolutionRules.ActionPool(
            OriginalResolutionBand.Goon, GangAction.Heal, 11));
        Assert.Equal(11, OriginalResolutionRules.ActionPool(
            OriginalResolutionBand.Standard, GangAction.Influence, 11));
    }

    [Fact]
    public void ActionThresholdsMatchAllRecoveredBands()
    {
        Assert.Equal(5, OriginalResolutionRules.SuccessThreshold(
            OriginalResolutionBand.Goon, GangAction.Heal));
        Assert.Equal(4, OriginalResolutionRules.SuccessThreshold(
            OriginalResolutionBand.Expert, GangAction.Heal));
        Assert.Equal(6, OriginalResolutionRules.SuccessThreshold(
            OriginalResolutionBand.Standard, GangAction.Research));
        Assert.Equal(5, OriginalResolutionRules.SuccessThreshold(
            OriginalResolutionBand.Expert, GangAction.Research));
        Assert.Equal(6, OriginalResolutionRules.SuccessThreshold(
            OriginalResolutionBand.Goon, GangAction.Attack));
        Assert.Equal(5, OriginalResolutionRules.SuccessThreshold(
            OriginalResolutionBand.Standard, GangAction.Attack));
        Assert.Equal(4, OriginalResolutionRules.SuccessThreshold(
            OriginalResolutionBand.Expert, GangAction.Attack));
    }

    [Fact]
    public void DiceSuccessThresholdIsInclusive()
    {
        int[] rolls = [1, 2, 3, 4, 5, 6];

        Assert.Equal(3, OriginalResolutionRules.CountSuccesses(rolls, 4));
        Assert.Equal(2, OriginalResolutionRules.CountSuccesses(rolls, 5));
        Assert.Equal(1, OriginalResolutionRules.CountSuccesses(rolls, 6));
    }

    [Fact]
    public void HiddenDetectionUsesRecoveredD20Thresholds()
    {
        var standard = OriginalResolutionRules.HiddenEvasionThreshold(
            OriginalResolutionBand.Standard, 8, 8);
        var expert = OriginalResolutionRules.HiddenEvasionThreshold(
            OriginalResolutionBand.Expert, 8, 8);

        Assert.Equal(14, standard);
        Assert.Equal(35, OriginalResolutionRules.HiddenHitPercent(standard));
        Assert.Equal(10, expert);
        Assert.Equal(55, OriginalResolutionRules.HiddenHitPercent(expert));
    }

    [Fact]
    public void CombatAndCrackdownCalibrationsUseOriginalIntegerTruncation()
    {
        Assert.Equal(6, OriginalResolutionRules.AdjustDefense(OriginalResolutionBand.Goon, 7));
        Assert.Equal(7, OriginalResolutionRules.AdjustDefense(OriginalResolutionBand.Standard, 7));
        Assert.Equal(2, OriginalResolutionRules.MainAttackDamage(10, 1));
        Assert.Equal(4, OriginalResolutionRules.MainAttackDamage(10, 4));
        Assert.Equal(6, OriginalResolutionRules.CrackdownContribution(
            OriginalResolutionBand.Expert, controlsSector: true, 7));
        Assert.Equal(7, OriginalResolutionRules.CrackdownContribution(
            OriginalResolutionBand.Expert, controlsSector: false, 7));
    }
}
