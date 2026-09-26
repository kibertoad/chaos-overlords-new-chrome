using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class LastTurnEventsLayoutTests
{
    [Fact]
    public void PointerTargetsFollowRecoveredLastTurnEventsHandler()
    {
        Assert.Equal(new Rectangle(135, 157, 26, 23), LastTurnEventsLayout.Previous);
        Assert.Equal(new Rectangle(163, 157, 26, 23), LastTurnEventsLayout.Next);
        Assert.Equal(new Rectangle(137, 293, 49, 22), LastTurnEventsLayout.Ok);
    }

    /// <summary>Every drawn element at the screen position SCR-EVENT-001 records.</summary>
    [Fact]
    public void DrawnElementsSitWhereScrEvent001PutsThem()
    {
        Assert.Equal(new Rectangle(104, 124, 344, 209), LastTurnEventsLayout.Panel);
        Assert.Equal(new Rectangle(138, 137, 12, 7), LastTurnEventsLayout.PageNumber);
        Assert.Equal(new Rectangle(174, 137, 12, 7), LastTurnEventsLayout.PageCount);
        Assert.Equal(new Rectangle(118, 363, 26, 23), LastTurnEventsLayout.PreviousSource(firstPage: false));
        Assert.Equal(new Rectangle(170, 363, 26, 23), LastTurnEventsLayout.PreviousSource(firstPage: true));
        Assert.Equal(new Rectangle(144, 363, 26, 23), LastTurnEventsLayout.NextSource(lastPage: false));
        Assert.Equal(new Rectangle(196, 363, 26, 23), LastTurnEventsLayout.NextSource(lastPage: true));
        Assert.Equal(new Rectangle(198, 135, 242, 158), LastTurnEventsLayout.Artwork);
        Assert.Equal(new Rectangle(296, 190, 48, 48), LastTurnEventsLayout.ResearchItem);
        Assert.Equal(new Rectangle(240, 237, 48, 48), LastTurnEventsLayout.EliminatedPortrait);
        Assert.Equal(new Rectangle(298, 298, 138, 7), LastTurnEventsLayout.SubjectBacking);
        Assert.Equal(new Rectangle(226, 307, 210, 7), LastTurnEventsLayout.CaptionBacking);
        Assert.Equal(new Rectangle(226, 298, 24, 7), LastTurnEventsLayout.Year);
        Assert.Equal(new Rectangle(256, 298, 12, 7), LastTurnEventsLayout.Week);
        Assert.Equal(new Point(298, 298), LastTurnEventsLayout.Subject);
        Assert.Equal(new Point(226, 307), LastTurnEventsLayout.Caption);
        Assert.Equal(35, LastTurnEventsLayout.CaptionColumns);

        // The separator and second name follow a two-cell sector label (SCR-EVENT-001).
        Assert.Equal(310, LastTurnEventsLayout.Subject.X + 2 * OriginalFontLayout.CellWidth);
        Assert.Equal(316, LastTurnEventsLayout.Subject.X + 3 * OriginalFontLayout.CellWidth);

        // The faces and the pointer rectangles cover the same pixels.
        Assert.True(LastTurnEventsLayout.Artwork.Contains(LastTurnEventsLayout.ResearchItem));
        Assert.True(LastTurnEventsLayout.Artwork.Contains(LastTurnEventsLayout.EliminatedPortrait));
        Assert.Equal(LastTurnEventsLayout.Previous.Size,
            LastTurnEventsLayout.PreviousSource(firstPage: false).Size);
        Assert.Equal(LastTurnEventsLayout.Next.Size,
            LastTurnEventsLayout.NextSource(lastPage: false).Size);
    }

    /// <summary>
    /// SCR-EVENT-001: the pressed faces of Previous, Next and Exit, where they are drawn, and the
    /// rectangle a release must end in for the button to act.
    /// </summary>
    [Fact]
    public void PressedFacesFollowScrEvent001()
    {
        Assert.Equal(new Rectangle(66, 363, 26, 23),
            LastTurnEventsLayout.PressedSource(LastTurnEventsButton.Previous));
        Assert.Equal(new Rectangle(92, 363, 26, 23),
            LastTurnEventsLayout.PressedSource(LastTurnEventsButton.Next));
        Assert.Equal(new Rectangle(50, 386, 50, 23),
            LastTurnEventsLayout.PressedSource(LastTurnEventsButton.Exit));
        Assert.Equal(new Rectangle(135, 157, 26, 23), LastTurnEventsLayout.Face(LastTurnEventsButton.Previous));
        Assert.Equal(new Rectangle(163, 157, 26, 23), LastTurnEventsLayout.Face(LastTurnEventsButton.Next));
        Assert.Equal(new Rectangle(137, 293, 50, 23), LastTurnEventsLayout.Face(LastTurnEventsButton.Exit));
        Assert.Equal(new Rectangle(137, 293, 49, 22), LastTurnEventsLayout.Hit(LastTurnEventsButton.Exit));
        foreach (var button in new[]
                 {
                     LastTurnEventsButton.Previous, LastTurnEventsButton.Next, LastTurnEventsButton.Exit
                 })
        {
            Assert.Equal(LastTurnEventsLayout.Face(button).Size,
                LastTurnEventsLayout.PressedSource(button).Size);
            Assert.True(LastTurnEventsLayout.Face(button).Contains(LastTurnEventsLayout.Hit(button)));
            Assert.Equal(button, LastTurnEventsLayout.ButtonAt(LastTurnEventsLayout.Hit(button).Location));
        }
        // The Exit face's last column and row are outside its pointer rectangle.
        Assert.Null(LastTurnEventsLayout.ButtonAt(new Point(186, 293)));
        Assert.Null(LastTurnEventsLayout.ButtonAt(new Point(137, 315)));
    }

    [Theory]
    [InlineData(1, "2050", "01")]
    [InlineData(52, "2050", "52")]
    [InlineData(53, "2051", "01")]
    [InlineData(105, "2052", "01")]
    public void DateIsElapsedTurnsAsYearAndWeek(int elapsedTurns, string year, string week)
    {
        Assert.Equal((year, week), LastTurnEventsLayout.Date(elapsedTurns));
    }

    [Fact]
    public void NoDateIsDrawnBeforeTheFirstResolution()
    {
        Assert.Null(LastTurnEventsLayout.Date(0));
    }
}
