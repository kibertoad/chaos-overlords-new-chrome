using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class HireAndEliminationTests
{
    [Fact]
    public void HireIsPaidAndQueuedBeforeDeferredPlacement()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        var cashBefore = match.Players[0].Cash;

        var result = match.QueueHire(new PlayerId(0), 2, 0);

        Assert.True(result.Accepted);
        Assert.Equal(string.Empty, result.Validation.Message);
        Assert.Equal(cashBefore - 1, match.Players[0].Cash);
        Assert.Equal(1, match.Players[0].Statistics.CashSpent);
        Assert.DoesNotContain((short)2, match.Players[0].HirePool);
        Assert.Equal(new PendingHireState(2, 0), Assert.Single(match.Players[0].PendingHires));
        Assert.Single(match.Players[0].Gangs);
        Assert.Equal(GameEventKind.HireQueued, result.Event!.Kind);
        Assert.Equal(2, result.Event.Hire!.GangDefinitionId);
    }

    [Fact]
    public void FinishingHirePlacesRecruitAndDeterministicallyRefillsPool()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);

        match.FinishHire(new PlayerId(0));

        var resolution = Assert.Single(match.LastHireResolutions);
        var recruit = match.FindGang(resolution.Gang)!;
        Assert.Equal((short)2, recruit.DefinitionId);
        Assert.Equal(0, recruit.SectorId);
        Assert.Equal(ManualRules.MaximumForce, recruit.Force);
        Assert.True(recruit.HiredThisTurn);
        Assert.Empty(match.Players[0].PendingHires);
        Assert.Equal(MatchLimits.HireOffersPerPlayer, match.Players[0].HirePool.Count);
        Assert.Equal(match.Players[0].HirePool.Count, match.Players[0].HirePool.Distinct().Count());
        Assert.Equal(3, match.Random.ConsumptionCount);
        Assert.Equal(GameEventKind.HireResolved, resolution.Event.Kind);
        Assert.Single(match.NotificationsFor(new PlayerId(0)), item => item.Kind == GameNotificationKind.Hire);

        FinishTurn(match);
        match.FinishUpkeep();
        Assert.False(recruit.HiredThisTurn);
    }

    [Fact]
    public void HireRulesReturnTheirOwnErrorMessagesWithoutMutation()
    {
        var match = CreateMatch();

        var wrongPhase = match.QueueHire(new PlayerId(0), 2, 0);

        Assert.Equal(HireValidationCode.InvalidPhase, wrongPhase.Validation.Code);
        Assert.Equal("Gangs may only be hired during the Hire phase.", wrongPhase.Validation.Message);
        Assert.Equal(10, match.Players[0].Cash);
        Assert.Empty(match.Events);

        AdvanceToHire(match);
        var cashBefore = match.Players[0].Cash;
        var eventCountBefore = match.Events.Count;
        var uncontrolled = match.QueueHire(new PlayerId(0), 2, 1);
        Assert.Equal(HireValidationCode.SectorNotControlled, uncontrolled.Validation.Code);
        Assert.Equal("A recruit must be placed in a sector controlled by the hiring player.",
            uncontrolled.Validation.Message);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Empty(match.Players[0].PendingHires);
        Assert.Equal(eventCountBefore, match.Events.Count);
    }

    [Fact]
    public void EndOfTurnEliminatesPlayerWithNoSectorOrActiveGangAndClearsInfluence()
    {
        var match = CreateMatch(influencedBySecondPlayer: true);
        AdvanceToHire(match);
        match.FinishHire(new PlayerId(0));
        match.FinishHire(new PlayerId(1));

        match.FinishPlayerElimination();

        Assert.Equal(PlayerStatus.Active, match.Players[0].Status);
        Assert.Equal(PlayerStatus.Eliminated, match.Players[1].Status);
        Assert.Null(match.Sectors[1].Sites[0].InfluencedBy);
        Assert.Equal(match.Definitions.Sites[0].Resistance, match.Sectors[1].Sites[0].Resistance);
        var gameEvent = Assert.Single(match.Events, item => item.Kind == GameEventKind.PlayerEliminated);
        Assert.Equal(new PlayerId(1), gameEvent.Elimination!.EliminatedPlayer);
        Assert.Equal(1, gameEvent.Elimination.RemainingPlayers);
        Assert.Contains(match.NotificationsFor(new PlayerId(0)), item => item.Kind == GameNotificationKind.Elimination);
        Assert.Contains(match.NotificationsFor(new PlayerId(1)), item => item.Kind == GameNotificationKind.Elimination);
        Assert.Equal(2, match.Coordinator.Turn);
        Assert.Equal(TurnPhase.Upkeep, match.Coordinator.Phase);
    }

    private static MatchState CreateMatch(bool influencedBySecondPlayer = false)
    {
        var definitions = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 10,
                [new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 5)],
                hirePool: [1, 2, 3]),
            new(setups[1], 10, hirePool: [4, 5, 6])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, id == 1 && influencedBySecondPlayer ? 0 : 7,
                    id == 1 && influencedBySecondPlayer ? new PlayerId(1) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? new PlayerId(0) : null))
            .ToArray();
        return new MatchState(definitions, setup, players, sectors);
    }

    private static void AdvanceToHire(MatchState match)
    {
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        Assert.Equal(TurnPhase.Hire, match.Coordinator.Phase);
    }

    private static void FinishTurn(MatchState match)
    {
        match.FinishHire(new PlayerId(1));
        match.FinishPlayerElimination();
    }
}
