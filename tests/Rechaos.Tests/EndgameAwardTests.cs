using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class EndgameAwardTests
{
    [Fact]
    public void AwardsUseNativeThresholdsPriorityAndPreserveTies()
    {
        MatchStatistics[] statistics =
        [
            new(cashSpent: 10, damageInflicted: 60, overthrows: 5, timesHidden: 10),
            new(cashSpent: 5, damageInflicted: 60, overthrows: 6),
            new(cashSpent: 5, overthrows: 6, timesHidden: 10)
        ];
        var match = CreateMatch(statistics);

        var awards = EndgameAwardEvaluator.Evaluate(match);

        Assert.Equal(
            [EndgameAward.Fist, EndgameAward.Skull, EndgameAward.BigFatChicken,
                EndgameAward.DollarSign, EndgameAward.Safe],
            awards.Select(result => result.Award));
        AssertAward(awards, EndgameAward.Fist, 6, 1, 2);
        AssertAward(awards, EndgameAward.Skull, 60, 0, 1);
        AssertAward(awards, EndgameAward.BigFatChicken, 10, 0, 2);
        AssertAward(awards, EndgameAward.DollarSign, 10, 0);
        AssertAward(awards, EndgameAward.Safe, 5, 1, 2);
    }

    [Fact]
    public void ValuesBelowNativeActivityThresholdsOmitThreeAwards()
    {
        var match = CreateMatch(new MatchStatistics(), new MatchStatistics());

        var awards = EndgameAwardEvaluator.Evaluate(match);

        Assert.DoesNotContain(awards, result => result.Award == EndgameAward.Skull);
        Assert.DoesNotContain(awards, result => result.Award == EndgameAward.BigFatChicken);
        Assert.DoesNotContain(awards, result => result.Award == EndgameAward.Fist);
        AssertAward(awards, EndgameAward.DollarSign, 0, 0, 1);
        AssertAward(awards, EndgameAward.Safe, 0, 0, 1);
    }

    [Fact]
    public void NativeActivityThresholdsAreInclusive()
    {
        var match = CreateMatch(
            new MatchStatistics(damageInflicted: 50, overthrows: 5, timesHidden: 10),
            new MatchStatistics(damageInflicted: 49, overthrows: 4, timesHidden: 9));

        var awards = EndgameAwardEvaluator.Evaluate(match);

        AssertAward(awards, EndgameAward.Fist, 5, 0);
        AssertAward(awards, EndgameAward.Skull, 50, 0);
        AssertAward(awards, EndgameAward.BigFatChicken, 10, 0);
    }

    [Fact]
    public void EliminatedPlayersRemainEligibleForNativeAwards()
    {
        var match = CreateMatch(
            [new MatchStatistics(), new MatchStatistics(overthrows: 8)],
            playerZeroControlsForty: false,
            eliminatedPlayers: new HashSet<int> { 1 });

        AssertAward(EndgameAwardEvaluator.Evaluate(match), EndgameAward.Fist, 8, 1);
    }

    [Fact]
    public void SafeUsesTheNativeInitialCeiling()
    {
        var atCeiling = CreateMatch(
            new MatchStatistics(cashSpent: 999_999),
            new MatchStatistics(cashSpent: 1_000_000));
        var aboveCeiling = CreateMatch(
            new MatchStatistics(cashSpent: 1_000_000),
            new MatchStatistics(cashSpent: 1_000_001));

        AssertAward(EndgameAwardEvaluator.Evaluate(atCeiling),
            EndgameAward.Safe, 999_999, 0);
        Assert.DoesNotContain(EndgameAwardEvaluator.Evaluate(aboveCeiling),
            award => award.Award == EndgameAward.Safe);
    }

    [Fact]
    public void PresentationKeepsOnlyFirstThreeNativePriorityAwardsPerPlayer()
    {
        var match = CreateMatch(
            new MatchStatistics(cashSpent: 10, damageInflicted: 50,
                overthrows: 5, timesHidden: 10),
            new MatchStatistics(cashSpent: 1));
        var awards = EndgameAwardEvaluator.Evaluate(match);
        var outcome = new MatchOutcome(ScenarioId.Big40, MatchEndReason.ObjectiveCompleted,
            1, [new PlayerId(0)], [], awards);

        Assert.Equal(
            [EndgameAward.Fist, EndgameAward.Skull, EndgameAward.BigFatChicken],
            EndgamePresentation.AwardsForPlayer(outcome, new PlayerId(0))
                .Select(result => result.Award));
        Assert.Throws<ArgumentOutOfRangeException>(() => EndgameLayout.Award(0, 3));
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
        bool playerZeroControlsForty,
        IReadOnlySet<int>? eliminatedPlayers = null)
    {
        var data = BundledOriginalData.Load();
        var setups = statistics.Select((_, id) =>
            new MatchPlayerSetup(new PlayerId(id), $"PLAYER {id + 1}", PlayerController.Human)).ToArray();
        var setup = new MatchSetup(ScenarioId.Big40, GameDuration.SixMonths, 1996, setups);
        var players = setups.Select((player, id) =>
        {
            var eliminated = eliminatedPlayers?.Contains(id) == true;
            return new MatchPlayerState(player, 500,
                [new MatchGangState(new GangId(id), player.Id, 0, id, eliminated ? 0 : 10)],
                status: eliminated ? PlayerStatus.Eliminated : PlayerStatus.Active,
                statistics: statistics[id]);
        }).ToArray();
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
