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

        GameplayTurnFlow.AdvanceToPlanning(replay);

        Assert.Equal(TurnPhase.Command, state.Coordinator.Phase);
        Assert.Equal(new PlayerId(1), state.Coordinator.ActivePlayer);
        Assert.Contains(replay.Steps, operation =>
            operation.Kind == ReplayOperationKind.FinishCommand
            && operation.Player == new PlayerId(0));
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
