using Microsoft.Xna.Framework;
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
        Assert.Equal(new Rectangle(104, 125, 344, 209), IdleGangWarningLayout.Panel);
        Assert.Equal(new Rectangle(136, 262, 49, 24), IdleGangWarningLayout.Cancel);
        Assert.Equal(new Rectangle(136, 294, 49, 24), IdleGangWarningLayout.Ok);
        Assert.False(IdleGangWarningLayout.Cancel.Intersects(IdleGangWarningLayout.Ok));
    }
}
