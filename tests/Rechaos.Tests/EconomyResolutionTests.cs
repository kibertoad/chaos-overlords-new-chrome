using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class EconomyResolutionTests
{
    [Fact]
    public void UpkeepCombinesSectorSiteAndGangEconomyForEveryPlayer()
    {
        var match = CreateMatch(playerZeroCash: 20);

        match.FinishUpkeep();

        var first = match.LastUpkeepResolutions[0];
        Assert.Equal(new PlayerId(0), first.Player);
        Assert.Equal(20, first.Details.PreviousCash);
        Assert.Equal(2, first.Details.SectorIncome);
        Assert.Equal(5, first.Details.SiteIncome);
        Assert.Equal(3, first.Details.GangUpkeep);
        Assert.Equal(24, first.Details.ResultCash);
        Assert.Equal(4, first.Details.NetChange);
        Assert.False(first.Details.IsInDebt);
        Assert.Equal(24, match.Players[0].Cash);

        Assert.Equal(2, match.LastUpkeepResolutions.Count);
        Assert.Equal([0L, 1L], match.Events.Select(item => item.Sequence));
        Assert.All(match.Events, item => Assert.Equal(GameEventKind.UpkeepResolved, item.Kind));
        Assert.Equal(2, match.NotificationsFor(new PlayerId(0)).Count + match.NotificationsFor(new PlayerId(1)).Count);
        Assert.Equal(TurnPhase.Command, match.Coordinator.Phase);
        Assert.Single(match.PhaseHashes);
    }

    [Fact]
    public void NegativeProjectedCashPersistsAndRecordsDebt()
    {
        var match = CreateMatch(playerZeroCash: 0);

        match.FinishUpkeep();

        var details = match.LastUpkeepResolutions[0].Details;
        Assert.Equal(4, details.ResultCash);
        Assert.False(details.IsInDebt);

        var noIncomeMatch = CreateMatch(playerZeroCash: 1, playerZeroOwnsSectors: false, playerZeroInfluencesSite: false);
        noIncomeMatch.FinishUpkeep();
        var debt = noIncomeMatch.LastUpkeepResolutions[0].Details;
        Assert.Equal(-2, debt.ResultCash);
        Assert.True(debt.IsInDebt);
        Assert.Equal(-2, noIncomeMatch.Players[0].Cash);
    }

    [Fact]
    public void EliminatedPlayersDoNotReceiveEconomyMutationOrEvents()
    {
        var match = CreateMatch(playerZeroCash: 20, eliminatePlayerOne: true);
        var before = match.Players[1].Cash;

        match.FinishUpkeep();

        Assert.Single(match.LastUpkeepResolutions);
        Assert.Equal(before, match.Players[1].Cash);
        Assert.Empty(match.NotificationsFor(new PlayerId(1)));
    }

    [Fact]
    public void EquivalentUpkeepProducesEquivalentEventsAndHash()
    {
        var first = CreateMatch(20);
        var second = CreateMatch(20);

        first.FinishUpkeep();
        second.FinishUpkeep();

        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    [Fact]
    public void EconomyProjectionMatchesUpkeepWithoutMutatingState()
    {
        var match = CreateMatch(20);
        var player = match.Players[0];
        var before = MatchStateHasher.ComputeSha256(match);

        var forecast = EconomyResolver.Project(match, player);

        Assert.Equal(before, MatchStateHasher.ComputeSha256(match));
        Assert.Equal(new EconomyForecast(20, 2, 5, 3, 24), forecast);

        match.FinishUpkeep();
        var resolved = match.LastUpkeepResolutions[0].Details;
        Assert.Equal(forecast.CurrentCash, resolved.PreviousCash);
        Assert.Equal(forecast.SectorIncome, resolved.SectorIncome);
        Assert.Equal(forecast.SiteIncome, resolved.SiteIncome);
        Assert.Equal(forecast.GangUpkeep, resolved.GangUpkeep);
        Assert.Equal(forecast.ResultCash, resolved.ResultCash);
    }

    private static MatchState CreateMatch(
        int playerZeroCash,
        bool playerZeroOwnsSectors = true,
        bool playerZeroInfluencesSite = true,
        bool eliminatePlayerOne = false)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], playerZeroCash,
            [
                new MatchGangState(new GangId(10), new PlayerId(0), 28, 0, 5)
            ]),
            new(setups[1], 10, status: eliminatePlayerOne ? PlayerStatus.Eliminated : PlayerStatus.Active)
        ];

        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(
                id,
                [
                    new MatchSiteState(0, id == 0 ? (short)3 : (short)0, 7,
                        id == 0 && playerZeroInfluencesSite ? new PlayerId(0) : null),
                    new MatchSiteState(1, 1, 5),
                    new MatchSiteState(2, 2, 4)
                ],
                owner: playerZeroOwnsSectors && id < 2 ? new PlayerId(0) : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
