using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class TurnCoordinatorTests
{
    [Fact]
    public void FullTurnVisitsEveryDocumentedPhaseInOrder()
    {
        var coordinator = new TurnCoordinator(playerCount: 2);
        Assert.Equal(TurnPhase.Upkeep, coordinator.Phase);

        coordinator.FinishUpkeep();
        Assert.Equal((TurnPhase.Command, 0), (coordinator.Phase, coordinator.ActivePlayer!.Value.Value));
        coordinator.FinishCommand(new PlayerId(0));
        Assert.Equal(1, coordinator.ActivePlayer!.Value.Value);
        coordinator.FinishCommand(new PlayerId(1));

        foreach (var phase in TurnStructure.ExecutionOrder)
        {
            Assert.Equal(TurnPhase.Execution, coordinator.Phase);
            Assert.Equal(phase, coordinator.ExecutionPhase);
            coordinator.FinishExecutionPhase();
        }

        Assert.Equal((TurnPhase.Hire, 0), (coordinator.Phase, coordinator.ActivePlayer!.Value.Value));
        coordinator.FinishHire(new PlayerId(0));
        coordinator.FinishHire(new PlayerId(1));
        Assert.Equal(TurnPhase.PlayerElimination, coordinator.Phase);

        var transition = coordinator.FinishPlayerElimination();
        Assert.Equal(2, coordinator.Turn);
        Assert.Equal(TurnPhase.Upkeep, coordinator.Phase);
        Assert.Equal(2, transition.Turn);
    }

    [Fact]
    public void OutOfOrderTransitionIsRejectedWithoutMutation()
    {
        var coordinator = new TurnCoordinator(playerCount: 2);

        Assert.Throws<InvalidOperationException>(() => coordinator.FinishCommand(new PlayerId(0)));
        Assert.Equal(TurnPhase.Upkeep, coordinator.Phase);
        Assert.Equal(1, coordinator.Turn);
    }

    [Fact]
    public void WrongPlayerCannotCompleteCommandPhase()
    {
        var coordinator = new TurnCoordinator(playerCount: 2);
        coordinator.FinishUpkeep();

        Assert.Throws<InvalidOperationException>(() => coordinator.FinishCommand(new PlayerId(1)));
        Assert.Equal(new PlayerId(0), coordinator.ActivePlayer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void PlayerCountIsBounded(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TurnCoordinator(count));
}
