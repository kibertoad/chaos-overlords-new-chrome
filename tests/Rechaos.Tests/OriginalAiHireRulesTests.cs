using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

/// <summary>RULE-AI-008 and RULE-AI-009, with the fields FND-AI-064 and FND-AI-065 read.</summary>
public sealed class OriginalAiHireRulesTests
{
    [Fact]
    public void ModeZeroMinimizesUpkeepAndRequiresNonnegativeChaos()
    {
        var offers = new[]
        {
            Gang(upkeep: 2, chaos: 0),
            Gang(upkeep: 1, chaos: -1),
            Gang(upkeep: 2, chaos: 3)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, requestedMode: 0, availableCash: 100));
    }

    [Fact]
    public void ModeZeroIgnoresControl()
    {
        var offers = new[]
        {
            Gang(upkeep: 4, chaos: 1, control: 1),
            Gang(upkeep: 3, chaos: 3, control: -1),
            Gang(upkeep: 4, chaos: 0, control: 3)
        };

        Assert.Equal(1, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.KillEmAll, requestedMode: 0, availableCash: 100));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GreedCapsControlAndInfluenceCandidatesAtThreeUpkeep(int mode)
    {
        var offers = new[]
        {
            Gang(upkeep: 2, control: 2, influence: 2),
            Gang(upkeep: 4, control: 9, influence: 9),
            Gang(upkeep: 3, control: 3, influence: 3)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, mode, availableCash: 100));
        Assert.Equal(1, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, mode, availableCash: 100));
    }

    [Fact]
    public void ModesOneAndTwoRankControlAndInfluence()
    {
        var offers = new[]
        {
            Gang(control: 5, influence: 1, heal: 9, research: 9),
            Gang(control: 1, influence: 5, heal: 9, research: 9),
            Gang(control: 0, influence: 0, heal: 9, research: 9)
        };

        Assert.Equal(0, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 1, availableCash: 100));
        Assert.Equal(1, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 2, availableCash: 100));
    }

    [Fact]
    public void ModeThreeAddsOnlyPositiveStrengthAndWeaponProficienciesToCombat()
    {
        var offers = new[]
        {
            Gang(combat: 4, blade: -8, range: 1),
            Gang(combat: 2, blade: 2, fighting: 2),
            Gang(combat: 6, martialArts: -9)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 3, availableCash: 100));

        var strong = new[] { Gang(combat: 6), Gang(combat: 3, strength: 4), Gang(combat: 1) };
        Assert.Equal(1, OriginalAiHireRules.SelectOfferIndex(
            strong, ScenarioId.Power, requestedMode: 3, availableCash: 100));
    }

    [Fact]
    public void ModeFourHasScenarioSpecificEligibilityAndTieRules()
    {
        var offers = new[]
        {
            Gang(upkeep: 3, techLevel: 5, research: 1),
            Gang(upkeep: 5, techLevel: 6, research: 0),
            Gang(upkeep: 4, techLevel: 4, research: 2)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, requestedMode: 4, availableCash: 100));
        Assert.Equal(0, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 4, availableCash: 100));
    }

    [Fact]
    public void ModeFiveRequiresAtLeastTenStealthAndLaterTieWins()
    {
        var offers = new[] { Gang(stealth: 9, detect: 20), Gang(stealth: 10), Gang(stealth: 10) };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 5, availableCash: 100));
    }

    [Fact]
    public void RichNonGreedModeZeroIsPromotedToCombatMode()
    {
        var offers = new[]
        {
            Gang(force: 10, upkeep: 0, combat: 1),
            Gang(force: 10, upkeep: 3, combat: 5),
            Gang(force: 10, upkeep: 2, combat: 2)
        };

        Assert.Equal(1, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 0, availableCash: 201));
        Assert.Equal(0, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, requestedMode: 0, availableCash: 201));
        Assert.Equal(0, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 0, availableCash: 200));
    }

    [Fact]
    public void UnaffordableWinnerDoesNotFallBack()
    {
        var offers = new[]
        {
            Gang(force: 4, combat: 2),
            Gang(force: 8, combat: 9),
            Gang(force: 3, combat: 1)
        };

        Assert.Null(OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, requestedMode: 3, availableCash: 7));
    }

    [Fact]
    public void GreedAlwaysRejectsFirstOfferAfterFailedHire()
    {
        var offers = new[]
        {
            Gang(upkeep: 9),
            Gang(upkeep: 1),
            Gang(upkeep: 2)
        };

        Assert.Equal(0, OriginalAiHireRules.SelectRejectedOfferIndex(offers, ScenarioId.Greed));
    }

    [Fact]
    public void OtherScenariosRejectLowestEfficiencyAndKeepFirstTie()
    {
        var offers = new[]
        {
            Gang(force: 1, upkeep: 1, combat: 1, techLevel: 10),
            Gang(force: 9, upkeep: 9, combat: 1, techLevel: 10),
            Gang(force: 9, upkeep: 9, combat: 1, techLevel: 10)
        };

        Assert.Equal(1, OriginalAiHireRules.SelectRejectedOfferIndex(offers, ScenarioId.Power));
    }

    [Fact]
    public void RejectionWeighsByTechLevelAndSumsChaosButNotStealth()
    {
        // Values: 2 * 3 * 20 / 3 = 40; 2 * 2 * 20 / 3 = 26; 1 * 9 * 20 / 3 = 60.
        var offers = new[]
        {
            Gang(techLevel: 2, combat: 1, chaos: 2, stealth: 0),
            Gang(techLevel: 2, combat: 2, stealth: 50),
            Gang(techLevel: 1, combat: 9)
        };

        Assert.Equal(1, OriginalAiHireRules.SelectRejectedOfferIndex(offers, ScenarioId.Power));
    }

    [Fact]
    public void RejectionFallsBackToTheFirstOfferWhenNoValueIsBelowTheStart()
    {
        var offers = new[]
        {
            Gang(force: 0, upkeep: 0, techLevel: 100, combat: 100),
            Gang(force: 0, upkeep: 0, techLevel: 100, combat: 90),
            Gang(force: 0, upkeep: 0, techLevel: 100, combat: 80)
        };

        Assert.Equal(0, OriginalAiHireRules.SelectRejectedOfferIndex(offers, ScenarioId.Power));
    }

    private static GangDefinition Gang(
        short force = 1,
        short upkeep = 1,
        short techLevel = 0,
        short combat = 0,
        short stealth = 0,
        short detect = 0,
        short chaos = 0,
        short control = 0,
        short heal = 0,
        short influence = 0,
        short research = 0,
        short strength = 0,
        short blade = 0,
        short range = 0,
        short fighting = 0,
        short martialArts = 0) =>
        new("TEST", 0, "", force, upkeep, techLevel,
            new Statistics(combat, 0, stealth, detect, chaos, control, heal, influence, research,
                strength, blade, range, fighting, martialArts));
}
