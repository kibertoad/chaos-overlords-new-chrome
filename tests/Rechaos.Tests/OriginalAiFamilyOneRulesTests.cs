using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyOneRulesTests
{
    [Fact]
    public void HealBoundariesPreserveBothRecoveredForceLimits()
    {
        Assert.True(OriginalAiFamilyOneRules.CanHeal(7, -3,
            OriginalAiFamilyOneRules.StrictHealForceLimit));
        Assert.False(OriginalAiFamilyOneRules.CanHeal(8, -3,
            OriginalAiFamilyOneRules.StrictHealForceLimit));
        Assert.True(OriginalAiFamilyOneRules.CanHeal(8, -3,
            OriginalAiFamilyOneRules.CommonHealForceLimit));
        Assert.False(OriginalAiFamilyOneRules.CanHeal(9, -3,
            OriginalAiFamilyOneRules.CommonHealForceLimit));
        Assert.False(OriginalAiFamilyOneRules.CanHeal(7, -4,
            OriginalAiFamilyOneRules.StrictHealForceLimit));
    }

    [Fact]
    public void StrictCashContinuationChangesBetweenFiftyAndFiftyOne()
    {
        Assert.Equal(GangAction.Move,
            OriginalAiFamilyOneRules.SelectStrictCashContinuation(50));
        Assert.Equal(GangAction.Snitch,
            OriginalAiFamilyOneRules.SelectStrictCashContinuation(51));
    }

    [Fact]
    public void CrimeChoicePreservesCashAndToleranceBoundaries()
    {
        Assert.Equal(GangAction.Move,
            OriginalAiFamilyOneRules.SelectCrimeOrMove(49, 3));
        Assert.Equal(GangAction.Chaos,
            OriginalAiFamilyOneRules.SelectCrimeOrMove(50, 3));
        Assert.Equal(GangAction.Snitch,
            OriginalAiFamilyOneRules.SelectCrimeOrMove(50, 4));
    }

    [Fact]
    public void NoActionOrChaosBranchHonorsCrackdownAndOlderSnitch()
    {
        Assert.Equal(GangAction.Heal,
            OriginalAiFamilyOneRules.SelectNoActionOrChaosContinuation(
                7, -3, crackdownActive: false, GangAction.None));
        Assert.Equal(GangAction.Move,
            OriginalAiFamilyOneRules.SelectNoActionOrChaosContinuation(
                7, -3, crackdownActive: true, GangAction.None));
        Assert.Equal(GangAction.Chaos,
            OriginalAiFamilyOneRules.SelectNoActionOrChaosContinuation(
                8, -3, crackdownActive: false, GangAction.Snitch));
        Assert.Equal(GangAction.Move,
            OriginalAiFamilyOneRules.SelectNoActionOrChaosContinuation(
                8, -3, crackdownActive: false, GangAction.None));
    }

    [Fact]
    public void HealContinuationRepeatsOrFallsThroughToControlThenMove()
    {
        Assert.Equal(GangAction.Heal,
            OriginalAiFamilyOneRules.SelectHealContinuation(
                8, -3, canSoloControl: false));
        Assert.Equal(GangAction.Control,
            OriginalAiFamilyOneRules.SelectHealContinuation(
                9, -3, canSoloControl: true));
        Assert.Equal(GangAction.Move,
            OriginalAiFamilyOneRules.SelectHealContinuation(
                8, -4, canSoloControl: false));
    }

    [Fact]
    public void PostEquipmentBranchPreservesOwnerCashMentalityAndToleranceComparisons()
    {
        var player = new PlayerId(0);

        Assert.Equal(GangAction.Move, Select(player, owner: 1, human: true,
            cash: 49, AiDifficulty.Criminal, tolerance: 3));
        Assert.Equal(GangAction.Move, Select(player, owner: 1, human: true,
            cash: 50, AiDifficulty.Goon, tolerance: 3));
        Assert.Equal(GangAction.Chaos, Select(player, owner: 1, human: true,
            cash: 50, AiDifficulty.Criminal, tolerance: 3));
        Assert.Equal(GangAction.Snitch, Select(player, owner: 1, human: true,
            cash: 50, AiDifficulty.HomicidalManiac, tolerance: 4));

        Assert.Equal(GangAction.Move, Select(player, owner: -1, human: false,
            cash: 50, AiDifficulty.Goon, tolerance: 3));
        Assert.Equal(GangAction.Move, Select(new PlayerId(1), owner: 0, human: false,
            cash: 50, AiDifficulty.Goon, tolerance: 3));
        Assert.Equal(GangAction.Move, Select(player, owner: 0, human: false,
            cash: 50, AiDifficulty.Goon, tolerance: 3));
        Assert.Equal(GangAction.Move, Select(player, owner: 1, human: false,
            cash: 49, AiDifficulty.Goon, tolerance: 3));
        Assert.Equal(GangAction.Chaos, Select(player, owner: 1, human: false,
            cash: 50, AiDifficulty.Goon, tolerance: 3));
        Assert.Equal(GangAction.Snitch, Select(player, owner: 1, human: false,
            cash: 50, AiDifficulty.Goon, tolerance: 4));
        Assert.Equal(GangAction.Move, Select(player, owner: 1, human: false,
            cash: 50, AiDifficulty.Criminal, tolerance: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Select(
            player, owner: MatchLimits.PlayerCount, human: false,
            cash: 50, AiDifficulty.Goon, tolerance: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Select(
            player, owner: 1, human: false,
            cash: 50, (AiDifficulty)99, tolerance: 3));
    }

    private static GangAction Select(
        PlayerId player,
        int owner,
        bool human,
        int cash,
        AiDifficulty mentality,
        int tolerance) => OriginalAiFamilyOneRules.SelectPostEquipmentContinuation(
            player, owner, human, cash, mentality, tolerance);
}
