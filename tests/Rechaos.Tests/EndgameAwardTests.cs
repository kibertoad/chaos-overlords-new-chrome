using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class EndgameAwardTests
{
    [Fact]
    public void AwardsUseManualStatisticsAndPreserveTies()
    {
        MatchStatistics[] statistics =
        [
            new(cashSpent: 10, damageInflicted: 12, overthrows: 1, timesHidden: 4),
            new(cashSpent: 5, damageInflicted: 12, overthrows: 2),
            new(cashSpent: 5, overthrows: 2, timesHidden: 4)
        ];
        var match = CreateMatch(statistics);

        var awards = EndgameAwardEvaluator.Evaluate(match);

        Assert.Equal(Enum.GetValues<EndgameAward>(), awards.Select(result => result.Award));
        AssertAward(awards, EndgameAward.Skull, 12, 0, 1);
        AssertAward(awards, EndgameAward.Fist, 2, 1, 2);
        AssertAward(awards, EndgameAward.DollarSign, 10, 0);
        AssertAward(awards, EndgameAward.Safe, 5, 1, 2);
        AssertAward(awards, EndgameAward.BigFatChicken, 4, 0, 2);
    }

    [Fact]
    public void ZeroCombatAndHidingOmitOnlyThoseActivityAwards()
    {
        var match = CreateMatch(new MatchStatistics(), new MatchStatistics());

        var awards = EndgameAwardEvaluator.Evaluate(match);

        Assert.DoesNotContain(awards, result => result.Award == EndgameAward.Skull);
        Assert.DoesNotContain(awards, result => result.Award == EndgameAward.BigFatChicken);
        AssertAward(awards, EndgameAward.Fist, 0, 0, 1);
        AssertAward(awards, EndgameAward.DollarSign, 0, 0, 1);
        AssertAward(awards, EndgameAward.Safe, 0, 0, 1);
    }

    [Fact]
    public void CompletedOutcomeCarriesAwardsIntoEventAndHash()
    {
        var match = CreateMatch(
            [new MatchStatistics(cashSpent: 8, damageInflicted: 3),
                new MatchStatistics(cashSpent: 2)],
            playerZeroControlsForty: true);

        FinishTurn(match);

        var outcome = Assert.IsType<MatchOutcome>(match.Outcome);
        Assert.Equal(outcome.Awards, match.Events.Single(value => value.Kind == GameEventKind.MatchEnded).MatchOutcome!.Awards);
        Assert.NotEmpty(outcome.Awards);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), match.PhaseHashes[^1].Sha256);
    }

    private static void AssertAward(
        IReadOnlyList<EndgameAwardResult> awards,
        EndgameAward award,
        long value,
        params int[] players)
    {
        var result = Assert.Single(awards, candidate => candidate.Award == award);
        Assert.Equal(value, result.Value);
        Assert.Equal(players.Select(id => new PlayerId(id)), result.Recipients);
    }

    private static MatchState CreateMatch(params MatchStatistics[] statistics) =>
        CreateMatch(statistics, playerZeroControlsForty: false);

    private static MatchState CreateMatch(
        MatchStatistics[] statistics,
        bool playerZeroControlsForty)
    {
        var data = BundledOriginalData.Load();
        var setups = statistics.Select((_, id) =>
            new MatchPlayerSetup(new PlayerId(id), $"PLAYER {id + 1}", PlayerController.Human)).ToArray();
        var setup = new MatchSetup(ScenarioId.Big40, GameDuration.SixMonths, 1996, setups);
        var players = setups.Select((player, id) =>
            new MatchPlayerState(player, 500,
                [new MatchGangState(new GangId(id), player.Id, 0, id, 10)],
                statistics: statistics[id])).ToArray();
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: playerZeroControlsForty && id < 40 ? new PlayerId(0) : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }

    private static void FinishTurn(MatchState match)
    {
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
    }
}
