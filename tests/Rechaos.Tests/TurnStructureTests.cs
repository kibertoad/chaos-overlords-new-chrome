using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class TurnStructureTests
{
    [Fact]
    public void TopLevelAndExecutionOrderMatchOriginalManual()
    {
        Assert.Equal(
            [TurnPhase.Upkeep, TurnPhase.Command, TurnPhase.Execution, TurnPhase.Hire, TurnPhase.PlayerElimination],
            TurnStructure.TurnOrder);
        Assert.Equal(
            [ExecutionPhase.Instant, ExecutionPhase.Combat, ExecutionPhase.Transaction,
             ExecutionPhase.Chaos, ExecutionPhase.Movement, ExecutionPhase.Control],
            TurnStructure.ExecutionOrder);
    }

    [Theory]
    [InlineData(GangAction.Bribe, ExecutionPhase.Instant)]
    [InlineData(GangAction.Heal, ExecutionPhase.Instant)]
    [InlineData(GangAction.Hide, ExecutionPhase.Instant)]
    [InlineData(GangAction.Influence, ExecutionPhase.Instant)]
    [InlineData(GangAction.Research, ExecutionPhase.Instant)]
    [InlineData(GangAction.Snitch, ExecutionPhase.Instant)]
    [InlineData(GangAction.Attack, ExecutionPhase.Combat)]
    [InlineData(GangAction.Equip, ExecutionPhase.Transaction)]
    [InlineData(GangAction.Give, ExecutionPhase.Transaction)]
    [InlineData(GangAction.Sell, ExecutionPhase.Transaction)]
    [InlineData(GangAction.Chaos, ExecutionPhase.Chaos)]
    [InlineData(GangAction.Move, ExecutionPhase.Movement)]
    [InlineData(GangAction.Terminate, ExecutionPhase.Movement)]
    [InlineData(GangAction.Control, ExecutionPhase.Control)]
    public void ActionsMapToDocumentedExecutionPhase(GangAction action, ExecutionPhase phase) =>
        Assert.Equal(phase, TurnStructure.PhaseFor(action));
}
