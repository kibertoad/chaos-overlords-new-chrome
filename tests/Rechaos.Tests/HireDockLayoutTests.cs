using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class HireDockLayoutTests
{
    [Fact]
    public void HirePriceSitsBesideRejectControl()
    {
        Assert.Equal(new Rectangle(472, 437, 32, 13), HireDockLayout.Reject(0));
        // SCR-HIRE-002, FND-HIRE-007: retain the original number helper's origin.
        Assert.Equal(new Point(450, 440), HireDockLayout.Price(0));
        Assert.Equal(new Point(516, 440), HireDockLayout.Price(1));
        Assert.Equal(new Point(582, 440), HireDockLayout.Price(2));
        // FND-UI-006: the console passes the helper's leading-zero flag.
        Assert.Equal("06", HireDockLayout.PriceCells(6).Digits);
        Assert.Equal("12", HireDockLayout.PriceCells(12).Digits);
        // FND-UI-023: two cells only; the first takes the whole quotient, 12 places after '0'.
        Assert.Equal("<3", HireDockLayout.PriceCells(123).Digits);
        // FND-UI-006: a negative value shows its magnitude; the colour carries the sign.
        Assert.Equal("07", HireDockLayout.PriceCells(-7).Digits);
        // RULE-UI-004: a quotient past the strip's last character leaves a blank in the text and
        // is drawn from the raw PX00129 cell of glyph 16 + 43.
        Assert.Equal(" 0", HireDockLayout.PriceCells(430).Digits);
        Assert.Equal(59, HireDockLayout.PriceCells(430).OffStripGlyph);
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("06", false, false), HireDockLayout.PriceCells(6));
    }

    [Fact]
    public void HireDockMatchesOriginalThreeCellStripAndRetainsHiredSlot()
    {
        Assert.Equal(new Rectangle(438, 370, 66, 90), HireDockLayout.Cell(0));
        // SCR-HIRE-002: portraits at (440 + 66s, 373), press regions that meet.
        Assert.Equal(new Rectangle(440, 373, 64, 64), HireDockLayout.Portrait(0));
        Assert.Equal(new Rectangle(572, 373, 64, 64), HireDockLayout.Portrait(2));
        Assert.Equal(new Rectangle(440, 373, 65, 64), HireDockLayout.PortraitHit(0));
        Assert.Equal(new Rectangle(505, 373, 66, 64), HireDockLayout.PortraitHit(1));
        Assert.Equal(new Rectangle(571, 373, 66, 64), HireDockLayout.PortraitHit(2));
        HireOfferSlotState[] offers =
        [
            HireOfferSlotState.Available(1),
            HireOfferSlotState.Available(2),
            HireOfferSlotState.Available(3)
        ];
        var cells = HireDockLayout.Project(offers, new PendingHireState(2, 12, 1), snubbedSlot: null);
        Assert.Equal(new HireDockEntry(1, HireDockMark.None), cells[0]);
        Assert.Equal(new HireDockEntry(2, HireDockMark.Hired), cells[1]);
        Assert.Equal(new HireDockEntry(3, HireDockMark.None), cells[2]);
        Assert.Equal(new Rectangle(604, 437, 32, 13), HireDockLayout.Reject(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireDockLayout.Cell(3));
    }

    [Fact]
    public void HireDockMarksTheSnubbedSlotForItsCross()
    {
        HireOfferSlotState[] offers =
        [
            HireOfferSlotState.Available(1),
            HireOfferSlotState.Available(2),
            HireOfferSlotState.Available(3)
        ];

        var cells = HireDockLayout.Project(offers, pending: null, snubbedSlot: 2);

        Assert.Equal(
            [HireDockMark.None, HireDockMark.None, HireDockMark.Snubbed],
            cells.Select(cell => cell!.Mark));
    }

    [Fact]
    public void HireDockCursorUsesPhysicalSlotsAndSkipsVacancies()
    {
        HireOfferSlotState[] offers =
        [
            HireOfferSlotState.Vacant(1),
            HireOfferSlotState.Available(2),
            HireOfferSlotState.Available(3)
        ];

        Assert.Equal(1, HireDockLayout.MoveCursor(offers, -1, 1));
        Assert.Equal(2, HireDockLayout.MoveCursor(offers, 1, 1));
        Assert.Equal(1, HireDockLayout.MoveCursor(offers, 2, 1));
        Assert.Equal(2, HireDockLayout.MoveCursor(offers, 1, -1));
        Assert.Equal(1, HireDockLayout.MoveCursor(offers, 0, 0));
        Assert.Equal(-1, HireDockLayout.MoveCursor(
            [HireOfferSlotState.Vacant(1), HireOfferSlotState.Vacant(2), HireOfferSlotState.Vacant(3)],
            1, 1));
    }
}
