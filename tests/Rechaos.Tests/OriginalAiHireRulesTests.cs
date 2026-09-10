using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiHireRulesTests
{
    [Fact]
    public void ModeZeroMinimizesUpkeepAndRequiresNonnegativeControl()
    {
        var offers = new[]
        {
            Gang(upkeep: 2, control: 0),
            Gang(upkeep: 1, control: -1),
            Gang(upkeep: 2, control: 3)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, requestedMode: 0, availableCash: 100));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void GreedCapsHealAndResearchCandidatesAtThreeUpkeep(int mode)
    {
        var offers = new[]
        {
            Gang(upkeep: 2, heal: 2, research: 2),
            Gang(upkeep: 4, heal: 9, research: 9),
            Gang(upkeep: 3, heal: 3, research: 3)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, mode, availableCash: 100));
        Assert.Equal(1, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, mode, availableCash: 100));
    }

    [Fact]
    public void ModeThreeAddsOnlyPositiveWeaponProficienciesToCombat()
    {
        var offers = new[]
        {
            Gang(combat: 4, blade: -8, range: 1),
            Gang(combat: 2, blade: 2, fighting: 2),
            Gang(combat: 6, martialArts: -9)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 3, availableCash: 100));
    }

    [Fact]
    public void ModeFourHasScenarioSpecificEligibilityAndTieRules()
    {
        var offers = new[]
        {
            Gang(upkeep: 3, stealth: 5, strength: 1),
            Gang(upkeep: 5, stealth: 6, strength: 0),
            Gang(upkeep: 4, stealth: 4, strength: 2)
        };

        Assert.Equal(2, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Greed, requestedMode: 4, availableCash: 100));
        Assert.Equal(0, OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 4, availableCash: 100));
    }

    [Fact]
    public void ModeFiveRequiresAtLeastTenDetectAndLaterTieWins()
    {
        var offers = new[] { Gang(detect: 9), Gang(detect: 10), Gang(detect: 10) };

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

    private static GangDefinition Gang(
        short force = 1,
        short upkeep = 1,
        short combat = 0,
        short stealth = 0,
        short detect = 0,
        short control = 0,
        short heal = 0,
        short research = 0,
        short strength = 0,
        short blade = 0,
        short range = 0,
        short fighting = 0,
        short martialArts = 0) =>
        new("TEST", 0, "", force, upkeep, 0,
            new Statistics(combat, 0, stealth, detect, 0, control, heal, 0, research,
                strength, blade, range, fighting, martialArts));
}
