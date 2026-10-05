using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SelectionFrameHoldTests
{
    [Fact]
    public void APanelComingInHoldsTheFrameShown()
    {
        // SCR-UI-003, FND-UI-051: the slide-in stops the pump's selection-frame copies.
        Assert.Equal(1, ChaosGame.HeldSelectionFrame(ClientScreen.City, ClientScreen.Search, held: null, shown: 1));
        Assert.Equal(0, ChaosGame.HeldSelectionFrame(ClientScreen.City, ClientScreen.CombatSummary, held: null, shown: 0));
    }

    [Fact]
    public void APanelReplacingAnotherKeepsTheHeldFrame() =>
        // FND-UI-051: no pass of the pump runs between the slide-out and the next slide-in.
        Assert.Equal(1, ChaosGame.HeldSelectionFrame(ClientScreen.Search, ClientScreen.Ranking, held: 1, shown: 0));

    [Fact]
    public void LeavingThePanelsReleasesTheFrame() =>
        // FND-UI-051: the slide-out clears the flag, and the pump draws the frame again.
        Assert.Null(ChaosGame.HeldSelectionFrame(ClientScreen.Search, ClientScreen.City, held: 1, shown: 1));

    [Fact]
    public void ScalingTakesThePixelUnderEachCentre()
    {
        // EXP-UI-008: a 4-by-4 source drawn 2 by 2 keeps the pixels at (1,1), (3,1), (1,3), (3,3).
        var pixels = Enumerable.Range(0, 16).Select(value => new Color(value, 0, 0)).ToArray();
        var scaled = PictureScaling.Scale(pixels, 4, new Rectangle(0, 0, 4, 4), new Point(2, 2));
        Assert.Equal([5, 7, 13, 15], scaled.Select(colour => (int)colour.R));
        // A 3-pixel span drawn 2 wide takes columns 0 and 2: (2x + 1) * 3 / 4.
        var row = PictureScaling.Scale(pixels, 4, new Rectangle(1, 2, 3, 1), new Point(2, 1));
        Assert.Equal([9, 11], row.Select(colour => (int)colour.R));
    }
}
