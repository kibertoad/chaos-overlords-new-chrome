using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ToleranceResolverTests
{
    [Theory]
    [InlineData(5, 12, 6)]
    [InlineData(12, 12, 12)]
    [InlineData(20, 12, 19)]
    public void UpkeepMovesToleranceOnePointTowardIncomeDerivedNormal(
        int current, int expectedNormal, int expected)
    {
        var match = CreateMatch(current, income: 5);

        Assert.Equal(expectedNormal, ToleranceResolver.NormalTolerance(match, match.Sectors[0]));
        match.FinishUpkeep();

        Assert.Equal(expected, match.Sectors[0].Tolerance);
    }

    [Fact]
    public void NormalToleranceIncludesOnlyInfluencedSiteAdjustments()
    {
        var match = CreateMatch(tolerance: 12, income: 5, influenceFirstTwoSites: true);

        // Condos add one, Corporate Towers subtract two, and the uninfluenced
        // Headquarters contribution is excluded: 17 - 5 + 1 - 2 = 11.
        Assert.Equal(11, ToleranceResolver.NormalTolerance(match, match.Sectors[0]));
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
            ], tolerance: id == 0 ? tolerance : 14, income: id == 0 ? income : 3))
            .ToArray();
        return new MatchState(data, setup, [player], sectors);
    }
}
