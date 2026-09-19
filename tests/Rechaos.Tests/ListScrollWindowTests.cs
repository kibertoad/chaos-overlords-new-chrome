using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The window both the drawing and the clicking of every online list read.
/// </summary>
/// <remarks>
/// Its whole point is that there is one answer to "which entry is row three", so the tests are
/// about the selection staying visible and the row-to-entry mapping being the same one either
/// caller would have computed.
/// </remarks>
public sealed class ListScrollWindowTests
{
    [Fact]
    public void AListShorterThanTheScreenStartsAtTheTopAndDrawsOnlyWhatItHas()
    {
        var window = ListScrollWindow.Of(total: 3, selection: 2, rows: 6);

        Assert.Equal(0, window.Offset);
        Assert.Equal(3, window.VisibleRows);
        Assert.False(window.Scrolls);
        Assert.Equal("3 SEATS", window.Tally("SEATS"));
    }

    [Fact]
    public void AnEmptyListDrawsNoRowsAndSaysNothingAboutHowMany()
    {
        var window = ListScrollWindow.Of(total: 0, selection: 0, rows: 6);

        Assert.Equal(0, window.VisibleRows);
        Assert.Equal(string.Empty, window.Tally("GAMES"));
    }

    /// <summary>A selection past the last drawn row pulls the window down to it, and no further.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 1)]
    [InlineData(11, 6)]
    public void TheWindowFollowsTheSelectionWithoutRunningOffTheEnd(int selection, int offset)
    {
        var window = ListScrollWindow.Of(total: 12, selection, rows: 6);

        Assert.Equal(offset, window.Offset);
        Assert.Equal(6, window.VisibleRows);
        Assert.InRange(selection, window.Offset, window.Offset + window.VisibleRows - 1);
    }

    [Fact]
    public void EveryDrawnRowStandsForTheEntryAClickOnItWouldSelect()
    {
        var window = ListScrollWindow.Of(total: 12, selection: 8, rows: 6);

        Assert.Equal(3, window.Offset);
        Assert.Equal([3, 4, 5, 6, 7, 8],
            Enumerable.Range(0, window.VisibleRows).Select(window.IndexAt));
    }

    [Fact]
    public void AListThatScrollsSaysWhichOfHowManyItIsShowing()
    {
        Assert.Equal("4-9 OF 12 GAMES", ListScrollWindow.Of(12, 8, 6).Tally("GAMES"));
        Assert.Equal("7-12 OF 12 GAMES", ListScrollWindow.Of(12, 11, 6).Tally("GAMES"));
    }
}
