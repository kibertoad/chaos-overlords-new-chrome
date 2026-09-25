using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Tests;

/// <summary>A two-seat match the persistence tests can drive without a scenario of their own.</summary>
internal static class TestMatches
{
    internal static MatchState Create(
        string firstPlayerName = "ONE",
        bool secondPlayerHuman = false,
        IReadOnlyList<int>? sectorZeroCrackdowns = null)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), firstPlayerName, PlayerController.Human),
            new(new PlayerId(1), "TWO",
                secondPlayerHuman ? PlayerController.Human : PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, playerSetups);
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id is 0 or 63 ? MatchBootstrap.HeadquartersDefinitionId : (short)0,
                    id is 0 or 63 ? 0 : 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], tolerance: ManualRules.MinimumTolerance,
                crackdownHistory: id == 0 ? sectorZeroCrackdowns : null))
            .ToArray();
        return MatchBootstrap.Create(data, setup, sectors,
        [
            new MatchPlayerStart(new PlayerId(0), 0, 10, 500, [2, 3, 4]),
            new MatchPlayerStart(new PlayerId(1), 63, 9, 500, [5, 6, 7])
        ]);
    }
}
