using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class TurnCommandQueueTests
{
    [Fact]
    public void PlanUsesDocumentedExecutionPhasesThenSubmissionOrder()
    {
        var queue = new TurnCommandQueue();
        queue.Set(Command(0, 10, GangAction.Move, CommandTarget.Sector(1)));
        queue.Set(Command(1, 11, GangAction.Bribe, CommandTarget.None));
        queue.Set(Command(0, 12, GangAction.Attack, CommandTarget.Gang(new GangId(99))));

        var plan = queue.ExecutionPlan();

        Assert.Equal([GangAction.Bribe, GangAction.Attack, GangAction.Move],
            plan.Select(entry => entry.Command.Action));
        Assert.Equal([ExecutionPhase.Instant, ExecutionPhase.Combat, ExecutionPhase.Movement],
            plan.Select(entry => entry.ExecutionPhase));
    }

    [Fact]
    public void SettingSecondCommandForGangReplacesFirstWithNewSequence()
    {
        var queue = new TurnCommandQueue();
        var first = queue.Set(Command(0, 10, GangAction.Move, CommandTarget.Sector(1)));
        var replacement = queue.Set(Command(0, 10, GangAction.Hide, CommandTarget.None));

        Assert.Equal(1, queue.Count);
        Assert.True(replacement.Sequence > first.Sequence);
        Assert.True(queue.TryGet(new GangId(10), out var stored));
        Assert.Equal(GangAction.Hide, stored!.Command.Action);
    }

    [Fact]
    public void CancelAndFinishExecutionHaveExplicitSemantics()
    {
        var queue = new TurnCommandQueue();
        queue.Set(Command(0, 10, GangAction.Hide, CommandTarget.None, repeat: true));
        queue.Set(Command(0, 11, GangAction.Move, CommandTarget.Sector(1)));
        queue.Set(Command(0, 12, GangAction.Snitch, CommandTarget.None));

        Assert.True(queue.Cancel(new GangId(12)));
        Assert.False(queue.Cancel(new GangId(12)));
        queue.FinishExecution();

        Assert.Single(queue.ExecutionPlan());
        Assert.True(queue.TryGet(new GangId(10), out var repeating));
        Assert.True(repeating!.Command.Repeat);
    }

    [Fact]
    public void NoneActionCannotBeQueued()
    {
        var queue = new TurnCommandQueue();
        Assert.Throws<ArgumentException>(() => queue.Set(Command(0, 10, GangAction.None, CommandTarget.None)));
        Assert.Empty(queue.ExecutionPlan());
    }

    [Fact]
    public void IdentifiersAndTargetsRejectOutOfRangeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerId(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerId(GameState.MaximumPlayers));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GangId(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommandTarget.Sector(64));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommandTarget.Site(192));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommandTarget.Item(64));
    }

    private static GameCommand Command(
        int player,
        int gang,
        GangAction action,
        CommandTarget target,
        bool repeat = false) => new(new PlayerId(player), new GangId(gang), action, target, repeat);
}
