using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GameplayTurnFlowTests
{
    [Fact]
    public void NormalFlowSkipsEliminatedPlayersAtPlanningBoundaries()
    {
        var definitions = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Human)
        ];
        var state = OriginalMatchFactory.Create(definitions,
            new MatchSetup(ScenarioId.Siege, GameDuration.SixMonths, 1996, players));
        state.Players[0].Status = PlayerStatus.Eliminated;
        var replay = new MatchReplayRecorder(state);

        var advance = GameplayTurnFlow.AdvanceToPlanning(replay);

        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        Assert.Equal(new PlayerId(1), state.Coordinator.ActivePlayer);
        Assert.Contains(replay.Steps, operation =>
            operation.Kind == ReplayOperationKind.FinishCommand
            && operation.Player == new PlayerId(0));
        Assert.Equal([new PlayerId(0)], advance.CrossedEliminatedPlayers);
    }

    [Fact]
    public void HotSeatEliminationPresentationAcknowledgesEachCrossedSeatOnlyOnce()
    {
        var presented = new HashSet<PlayerId>();

        Assert.Equal([new PlayerId(2), new PlayerId(4)],
            HotSeatEliminationPresentation.QueueUnpresented(
                [new PlayerId(2), new PlayerId(4)], presented));
        Assert.Empty(HotSeatEliminationPresentation.QueueUnpresented(
            [new PlayerId(2), new PlayerId(4)], presented));
        Assert.Equal([new PlayerId(5)], HotSeatEliminationPresentation.QueueUnpresented(
            [new PlayerId(4), new PlayerId(5)], presented));
    }

    [Fact]
    public void PrivateHandoffRequiresTwoActiveHumanSeats()
    {
        var definitions = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "LOCAL", PlayerController.Human),
            new(new PlayerId(1), "CPU", PlayerController.Computer),
            new(new PlayerId(2), "ELIMINATED", PlayerController.Human)
        ];
        var state = OriginalMatchFactory.Create(definitions,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
        state.Players[2].Status = PlayerStatus.Eliminated;

        Assert.False(HotSeatHandoffPresentation.RequiresPrivateHandoff(state));

        state.Players[2].Status = PlayerStatus.Active;

        Assert.True(HotSeatHandoffPresentation.RequiresPrivateHandoff(state));
    }

    // RULE-OBJECTIVE-005: a human eliminated in the turn just resolved still counts toward the
    // Ready card in the round its elimination card is shown; the round after, it does not.
    [Fact]
    public void HumanEliminatedLastTurnStillCountsForTheReadyCard()
    {
        var definitions = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "LOCAL", PlayerController.Human),
            new(new PlayerId(1), "OUT", PlayerController.Human),
            new(new PlayerId(2), "CPU", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 500, [new MatchGangState(new GangId(10), new PlayerId(0), 0, 0, 10)]),
            new(setups[1], 500, [new MatchGangState(new GangId(20), new PlayerId(1), 0, 1, 0)]),
            new(setups[2], 500, [new MatchGangState(new GangId(30), new PlayerId(2), 0, 2, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        var state = new MatchState(definitions, setup, players, sectors);

        FinishWholeTurn(state);
        Assert.Equal(PlayerStatus.Eliminated, state.Players[1].Status);
        Assert.True(HotSeatHandoffPresentation.RequiresPrivateHandoff(state));
        Assert.False(HotSeatEliminationPresentation.HasLaterLocalHuman(state, new PlayerId(1)));
        Assert.False(HotSeatEliminationPresentation.HasLaterLocalHuman(state, new PlayerId(0)));

        FinishWholeTurn(state);
        Assert.False(HotSeatHandoffPresentation.RequiresPrivateHandoff(state));
    }

    private static void FinishWholeTurn(MatchState state)
    {
        state.FinishUpkeep();
        foreach (var player in state.Players) state.FinishCommand(player.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) state.FinishExecutionPhase();
        foreach (var player in state.Players) state.FinishHire(player.Id);
        state.FinishPlayerElimination();
    }

    [Fact]
    public void NormalFlowStopsOnlyForPlayerPlanningAndResolvesInternalPhases()
    {
        var definitions = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Human)
        ];
        var state = OriginalMatchFactory.Create(definitions,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
        var replay = new MatchReplayRecorder(state);

        GameplayTurnFlow.AdvanceToPlanning(replay);

        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        Assert.Equal(new PlayerId(0), state.Coordinator.ActivePlayer);
        replay.PrepareHireOffers(new PlayerId(0));
        Assert.Equal(MatchLimits.HireOffersPerPlayer, state.Players[0].HirePool.Count);
        var offer = state.Players[0].HirePool[0];
        var sector = state.Players[0].Gangs[0].SectorId;
        Assert.True(replay.QueueHire(new PlayerId(0), offer, sector).Accepted);

        GameplayTurnFlow.FinishPlanningTurn(replay, new PlayerId(0));

        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        Assert.Equal(new PlayerId(1), state.Coordinator.ActivePlayer);
        Assert.Single(state.Players[0].PendingHires);
        replay.PrepareHireOffers(new PlayerId(1));
        Assert.Equal(MatchLimits.HireOffersPerPlayer, state.Players[1].HirePool.Count);

        GameplayTurnFlow.FinishPlanningTurn(replay, new PlayerId(1));

        Assert.Equal(1, state.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        Assert.Equal(new PlayerId(2), state.Coordinator.ActivePlayer);
        for (var player = 2; player < MatchLimits.PlayerCount; player++)
            GameplayTurnFlow.FinishPlanningTurn(replay, new PlayerId(player));

        Assert.Equal(2, state.Coordinator.Turn);
        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        Assert.Equal(new PlayerId(0), state.Coordinator.ActivePlayer);
        Assert.Empty(state.Players[0].PendingHires);
        Assert.Equal(2, state.Players[0].Gangs.Count);
        Assert.Contains(state.Events, gameEvent => gameEvent.Kind == GameEventKind.HireResolved
            && gameEvent.Player == new PlayerId(0));
    }
}
