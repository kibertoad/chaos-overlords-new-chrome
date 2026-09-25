using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ToleranceResolverTests
{
    // RULE-TOLERANCE-001: one point toward 17 minus Income, whatever the distance.
    [Theory]
    [InlineData(5, 6)]
    [InlineData(12, 12)]
    [InlineData(20, 19)]
    public void ResolutionOpensByMovingTheBaseOnePointTowardSeventeenMinusIncome(
        int current, int expected)
    {
        var match = CreateMatch(current, income: 5);

        ToleranceResolver.StepTowardNormal(match);

        Assert.Equal(12, ToleranceResolver.NormalBaseTolerance(match.Sectors[0]));
        Assert.Equal(expected, match.Sectors[0].BaseTolerance);
    }

    // RULE-TOLERANCE-001: the sites take no part in the step.
    [Fact]
    public void TheStepIgnoresTheSitesTolerance()
    {
        var match = CreateMatch(tolerance: 12, income: 5, influenceFirstTwoSites: true);

        ToleranceResolver.StepTowardNormal(match);

        Assert.Equal(12, match.Sectors[0].BaseTolerance);
    }

    // RULE-SITE-001: before planning, Tolerance is the base plus the completed sites.
    [Fact]
    public void TheRebuildBeforePlanningAddsTheCompletedSitesToTheBase()
    {
        var match = CreateMatch(tolerance: 12, income: 5, influenceFirstTwoSites: true);

        match.FinishUpkeep();

        // Condos add one, Corporate Towers subtract two, and the automatically controlled
        // Headquarters adds two: 12 + 1 - 2 + 2 = 13.
        Assert.Equal(1, ToleranceResolver.SiteAdjustment(match, match.Sectors[0]));
        Assert.Equal(12, match.Sectors[0].BaseTolerance);
        Assert.Equal(13, match.Sectors[0].Tolerance);
    }

    // RULE-BRIBE-001, RULE-SNITCH-001: plus or minus 3 on the base, stored as a signed byte.
    [Theory]
    [InlineData(10, 13, 7)]
    [InlineData(126, -127, 123)]
    [InlineData(-127, -124, 126)]
    public void BribeAndSnitchMoveTheBaseByThreeAndWrapTheSignedByte(
        int baseTolerance, int bribed, int snitched)
    {
        var match = CreateMatch(tolerance: 12, income: 5);
        var sector = match.Sectors[0];
        sector.BaseTolerance = baseTolerance;

        Assert.Equal(bribed, ToleranceResolver.ApplyBribe(match, sector));
        Assert.Equal(snitched, ToleranceResolver.ApplySnitch(match, sector));
    }

    // RULE-TOLERANCE-002: after the instant phase every base is clamped to 1..40.
    [Theory]
    [InlineData(-4, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(40, 40)]
    [InlineData(43, 40)]
    public void TheInstantPhaseClampsEveryBaseToOneThroughForty(int baseTolerance, int expected)
    {
        var match = CreateMatch(tolerance: 12, income: 5);
        match.Sectors[0].BaseTolerance = baseTolerance;

        ToleranceResolver.ClampAfterInstant(match);

        Assert.Equal(expected, match.Sectors[0].BaseTolerance);
    }

    private static MatchState CreateMatch(
        int tolerance,
        int income,
        bool influenceFirstTwoSites = false)
    {
        var data = BundledOriginalData.Load();
        var playerId = new PlayerId(0);
        var playerSetup = new MatchPlayerSetup(playerId, "ONE", PlayerController.Human);
        var setup = new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996, [playerSetup]);
        var player = new MatchPlayerState(playerSetup, 10,
            [new MatchGangState(new GangId(10), playerId, 1, 0, 5)]);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id == 0 ? (short)6 : (short)0, 0,
                    id == 0 && influenceFirstTwoSites ? playerId : null),
                new MatchSiteState(1, id == 0 ? (short)3 : (short)1, 0,
                    id == 0 && influenceFirstTwoSites ? playerId : null),
                new MatchSiteState(2, id == 0 ? (short)21 : (short)2, 0)
            ], owner: id == 0 && influenceFirstTwoSites ? playerId : null,
                tolerance: id == 0 ? tolerance : 14, income: id == 0 ? income : 3))
            .ToArray();
        return new MatchState(data, setup, [player], sectors);
    }
}
