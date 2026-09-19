using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CommandActionTooltipTests
{
    [Fact]
    public void EveryCommandOverlayEntryExplainsItself()
    {
        foreach (var action in CommandOverlayLayout.Actions)
        {
            var lines = CommandActionTooltips.Lines(action);

            Assert.True(lines.Count >= 2);
            Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
            Assert.All(lines, line => Assert.Equal(line.ToUpperInvariant(), line));
        }
    }

    [Fact]
    public void TooltipsAreNarrowEnoughForTheVirtualScreen()
    {
        foreach (var action in CommandOverlayLayout.Actions)
        {
            var lines = CommandActionTooltips.Lines(action);
            var bounds = HoverTooltipLayout.Bounds(new Point(320, 230), lines);

            Assert.True(bounds.Width <= VirtualInput.Width - 16);
            Assert.All(lines,
                line => Assert.True(line.Length * OriginalFontLayout.CellWidth + 16 <= bounds.Width));
        }
    }

    [Fact]
    public void TheFirstLineNamesTheActionAndTheIdleEntryReadsAsAnOrder()
    {
        Assert.Equal("INFLUENCE", CommandActionTooltips.Lines(GangAction.Influence)[0]);
        Assert.Equal("NO ORDER", CommandActionTooltips.Lines(GangAction.None)[0]);
    }

    [Theory]
    [InlineData(GangAction.Bribe, "$3")]
    [InlineData(GangAction.Bribe, "TOLERANCE BY 3")]
    [InlineData(GangAction.Snitch, "TOLERANCE BY 3")]
    [InlineData(GangAction.Heal, "RESTORES FORCE TO 10")]
    [InlineData(GangAction.Move, "SIX")]
    [InlineData(GangAction.Terminate, "UPKEEP")]
    public void TooltipsQuoteTheRuleValuesThatDriveTheCommand(GangAction action, string expected)
    {
        Assert.Contains(CommandActionTooltips.Lines(action), line => line.Contains(expected));
    }

    [Fact]
    public void RowsResolveOnlyInsideTheOneOffActionList()
    {
        var actions = CommandOverlayLayout.ActionsFor(recurring: false);
        for (var index = 0; index < actions.Count; index++)
            Assert.Equal(index, CommandActionTooltips.RowAt(
                CommandOverlayLayout.ActionRow(index).Center, recurring: false));

        Assert.Null(CommandActionTooltips.RowAt(new Point(0, 0), recurring: false));
        var gap = CommandOverlayLayout.ActionRow(0);
        Assert.Null(CommandActionTooltips.RowAt(
            new Point(gap.Center.X, gap.Bottom + 1), recurring: false));
    }

    [Fact]
    public void RecurringRowsAddressTheShorterRepeatableList()
    {
        var recurring = CommandOverlayLayout.ActionsFor(recurring: true);
        Assert.True(recurring.Count < CommandOverlayLayout.Actions.Count);

        var last = recurring.Count - 1;
        Assert.Equal(last, CommandActionTooltips.RowAt(
            CommandOverlayLayout.ActionRow(last).Center, recurring: true));
        Assert.Null(CommandActionTooltips.RowAt(
            CommandOverlayLayout.ActionRow(recurring.Count).Center, recurring: true));
    }

    [Fact]
    public void ATooltipAppearsOnlyAfterOneSecondOnTheSameRow()
    {
        var dwell = new HoverDwellTracker();
        Assert.Equal(TimeSpan.FromSeconds(1), HoverDwellTracker.Delay);

        dwell.Update(3, TimeSpan.FromSeconds(10));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(3, TimeSpan.FromSeconds(10.9));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(3, TimeSpan.FromSeconds(11));
        Assert.Equal(3, dwell.SettledRegion);
        dwell.Update(3, TimeSpan.FromSeconds(20));
        Assert.Equal(3, dwell.SettledRegion);
    }

    [Fact]
    public void LeavingOrChangingRowRestartsTheDwell()
    {
        var dwell = new HoverDwellTracker();
        dwell.Update(3, TimeSpan.FromSeconds(1));
        dwell.Update(3, TimeSpan.FromSeconds(4));
        Assert.Equal(3, dwell.SettledRegion);

        dwell.Update(4, TimeSpan.FromSeconds(4.1));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(5));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(5.1));
        Assert.Equal(4, dwell.SettledRegion);

        dwell.Update(null, TimeSpan.FromSeconds(6.2));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(6.3));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(7.4));
        Assert.Equal(4, dwell.SettledRegion);

        dwell.Cancel();
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(8.5));
        Assert.Null(dwell.SettledRegion);
    }

    [Fact]
    public void TheDwellRejectsNegativeRegionsAndTimestamps()
    {
        var dwell = new HoverDwellTracker();
        Assert.Throws<ArgumentOutOfRangeException>(() => dwell.Update(-1, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => dwell.Update(0, TimeSpan.FromSeconds(-1)));
    }
}
