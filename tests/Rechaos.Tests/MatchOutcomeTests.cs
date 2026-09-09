using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class MatchOutcomeTests
{
    [Fact]
    public void ProjectionUsesAuthoritativeSectorsHeadquartersAndRightHands()
    {
        var match = CreateMatch(ScenarioId.Siege, headquartersOwners: [0, 0, 0, 0, 0, 0]);

        var score = MatchOutcomeEvaluator.Project(match, match.Players[0]);

        Assert.Equal(6, score.ControlledSectors);
        Assert.Equal(6, score.ImportantSectorsControlled);
        Assert.Equal(1, score.OpponentsAlive);
        Assert.Equal(1, score.OpposingRightHandsAlive);
    }

    [Fact]
    public void ObjectiveOutcomeIsRecordedAtEliminationBoundary()
    {
        var match = CreateMatch(ScenarioId.Big40, playerZeroControlledSectors: 40);

        FinishTurn(match);

        var outcome = Assert.IsType<MatchOutcome>(match.Outcome);
        Assert.Equal(ScenarioId.Big40, outcome.Scenario);
        Assert.Equal(MatchEndReason.ObjectiveCompleted, outcome.Reason);
        Assert.Equal(1, outcome.Turn);
        Assert.Equal([new PlayerId(0)], outcome.Winners);
        var gameEvent = Assert.Single(match.Events, value => value.Kind == GameEventKind.MatchEnded);
        Assert.Equal(outcome.Winners, gameEvent.MatchOutcome!.Winners);
        Assert.All(match.Players, player => Assert.Contains(
            match.NotificationsFor(player.Id), notification =>
                notification.Kind == GameNotificationKind.Objective
                && notification.RelatedEventSequence == gameEvent.Sequence));
    }

    [Fact]
    public void EliminateScenarioEndsWhenOpposingRightHandsIsGone()
    {
        var match = CreateMatch(ScenarioId.Eliminate, playerOneHasRightHands: false);

        FinishTurn(match);

        Assert.Equal([new PlayerId(0)], match.Outcome!.Winners);
        Assert.Equal(PlayerStatus.Eliminated, match.Players[1].Status);
    }

    [Fact]
    public void TimedScenarioPreservesTiedWinnersAtExactTurnLimit()
    {
        var match = CreateMatch(
            ScenarioId.Greed, playerZeroControlledSectors: 1, playerOneControlledSectors: 1);

        for (var turn = 1; turn <= ScenarioCatalog.Turns(GameDuration.SixMonths); turn++)
        {
            FinishTurn(match);
            if (turn < ScenarioCatalog.Turns(GameDuration.SixMonths)) Assert.Null(match.Outcome);
        }

        Assert.Equal(26, match.Outcome!.Turn);
        Assert.Equal(MatchEndReason.TimeLimit, match.Outcome.Reason);
        Assert.Equal([new PlayerId(0), new PlayerId(1)], match.Outcome.Winners);
        Assert.Equal(
            [new MatchStanding(new PlayerId(0), 1, match.Players[0].Cash),
                new MatchStanding(new PlayerId(1), 1, match.Players[1].Cash)],
            match.Outcome.Standings);
        Assert.Single(match.Events, value => value.Kind == GameEventKind.MatchEnded);
    }

    [Fact]
    public void OutcomeParticipatesInCanonicalStateHash()
    {
        var unfinished = CreateMatch(ScenarioId.Big40, playerZeroControlledSectors: 40);
        var finished = CreateMatch(ScenarioId.Big40, playerZeroControlledSectors: 40);

        FinishTurn(finished);

        Assert.NotEqual(MatchStateHasher.ComputeSha256(unfinished), MatchStateHasher.ComputeSha256(finished));
    }

    [Fact]
    public void CompletedMatchCannotAdvanceAnotherTurn()
    {
        var match = CreateMatch(ScenarioId.Big40, playerZeroControlledSectors: 40);
        FinishTurn(match);

        var error = Assert.Throws<InvalidOperationException>(() => match.FinishUpkeep());

        Assert.Contains("ended", error.Message);
        Assert.Equal(2, match.Coordinator.Turn);
        Assert.Equal(TurnPhase.Upkeep, match.Coordinator.Phase);
    }

    private static void FinishTurn(MatchState match)
    {
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
    }

    private static MatchState CreateMatch(
        ScenarioId scenario,
        int playerZeroControlledSectors = 0,
        int playerOneControlledSectors = 0,
        bool playerOneHasRightHands = true,
        int[]? headquartersOwners = null)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(scenario, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 500,
                [new MatchGangState(new GangId(10), new PlayerId(0), 0, 0, 10)]),
            new(setups[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), playerOneHasRightHands ? (short)0 : (short)1, 1, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id =>
            {
                var headquartersOwner = headquartersOwners is not null && id < headquartersOwners.Length
                    ? headquartersOwners[id]
                    : -1;
                PlayerId? owner = headquartersOwner >= 0
                    ? new PlayerId(headquartersOwner)
                    : id < playerZeroControlledSectors
                        ? new PlayerId(0)
                        : id < playerZeroControlledSectors + playerOneControlledSectors
                            ? new PlayerId(1)
                            : null;
                return new MatchSectorState(id,
                [
                    new MatchSiteState(0, headquartersOwner >= 0 ? (short)21 : (short)0,
                        headquartersOwner >= 0 ? 0 : 7),
                    new MatchSiteState(1, 1, 5),
                    new MatchSiteState(2, 2, 4)
                ], owner);
            })
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
