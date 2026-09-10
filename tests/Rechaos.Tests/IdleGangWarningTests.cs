using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class IdleGangWarningTests
{
    private static readonly PlayerId Player = new(0);

    [Fact]
    public void EnabledPolicyWarnsForAnActiveUnassignedGang()
    {
        var idle = new MatchGangState(new GangId(1), Player, 0, 0, 10);

        Assert.True(IdleGangWarningPolicy.ShouldWarn(true, [idle]));
        Assert.False(IdleGangWarningPolicy.ShouldWarn(false, [idle]));
    }

    [Fact]
    public void InactiveRosterSlotsDoNotTriggerWarning()
    {
        var inactive = new MatchGangState(new GangId(1), Player, 0, 0, 0);

        Assert.False(IdleGangWarningPolicy.ShouldWarn(true, [inactive]));
    }

    [Fact]
    public void AssignedActiveGangDoesNotTriggerWarning()
    {
        var assigned = new MatchGangState(new GangId(1), Player, 0, 0, 10)
        {
            QueuedCommand = new QueuedCommand(
                0, new GameCommand(Player, new GangId(1), GangAction.Hide, CommandTarget.None))
        };

        Assert.False(IdleGangWarningPolicy.ShouldWarn(true, [assigned]));
    }

    [Fact]
    public void WarningButtonsAreDistinctAndInsideTheModal()
    {
        Assert.True(IdleGangWarningLayout.Panel.Contains(IdleGangWarningLayout.Continue));
        Assert.True(IdleGangWarningLayout.Panel.Contains(IdleGangWarningLayout.GoBack));
        Assert.False(IdleGangWarningLayout.Continue.Intersects(IdleGangWarningLayout.GoBack));
    }
}
