using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
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
        Assert.Equal(new Rectangle(104, 124, 344, 209), IdleGangWarningLayout.Panel);
        Assert.Equal(new Rectangle(137, 261, 49, 22), IdleGangWarningLayout.Cancel);
        Assert.Equal(new Rectangle(137, 293, 49, 22), IdleGangWarningLayout.Ok);
        Assert.False(IdleGangWarningLayout.Cancel.Intersects(IdleGangWarningLayout.Ok));
    }

    [Theory]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Execute)]
    public void NativeConfirmationKeysAreAccepted(Keys key) =>
        Assert.Equal(IdleGangWarningChoice.Confirm,
            IdleGangWarningPolicy.KeyboardChoice(new KeyboardState(key), default));

    [Theory]
    [InlineData(Keys.Y)]
    [InlineData(Keys.N)]
    [InlineData(Keys.Back)]
    [InlineData(Keys.Escape)]
    public void OtherKeysDoNotConfirm(Keys key) =>
        Assert.NotEqual(IdleGangWarningChoice.Confirm,
            IdleGangWarningPolicy.KeyboardChoice(new KeyboardState(key), default));

    [Fact]
    public void OnlyEscapeCancels()
    {
        Assert.Equal(IdleGangWarningChoice.Cancel,
            IdleGangWarningPolicy.KeyboardChoice(new KeyboardState(Keys.Escape), default));
        Assert.Equal(IdleGangWarningChoice.None,
            IdleGangWarningPolicy.KeyboardChoice(new KeyboardState(Keys.Back), default));
        Assert.Equal(IdleGangWarningChoice.None,
            IdleGangWarningPolicy.KeyboardChoice(new KeyboardState(Keys.N), default));
    }
}
