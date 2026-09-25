using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class HireDockLayoutTests
{
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
        Assert.Equal(new Rectangle(570, 436, 33, 24), HireDockLayout.PriceCell(2));
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
        Assert.False(cells[2]!.Hired);
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
