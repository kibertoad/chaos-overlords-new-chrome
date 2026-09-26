using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The pressed key faces and the cell and site flashes, which pause on the 166 ms presentation
/// clock (RULE-TIMER-004, FND-UI-019, FND-UI-017, FND-UI-018).
/// </summary>
public sealed class TickedPresentationTests
{
    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    [Fact]
    public void APressedKeyFaceShowsUntilTheNextTickAndThenActs()
    {
        var step = new TickedPresentation();
        var acted = 0;
        step.Start(TickedPresentationKind.KeyFace, new Rectangle(137, 293, 50, 23),
            PressedKeyFaces.Source(PressedKeyFace.Confirm), () => acted++, Ms(200));

        Assert.True(step.Active);
        Assert.True(step.Lit(Ms(200)));
        Assert.True(step.Lit(Ms(331)));
        Assert.False(step.TryFinish(Ms(331), out _));

        // Tick 2 falls at 332 ms: the one wait ends there.
        Assert.True(step.TryFinish(Ms(332), out var then));
        then!();
        Assert.Equal(1, acted);
        Assert.False(step.Active);
        Assert.False(step.Lit(Ms(332)));
    }

    /// <summary>
    /// RULE-TIMER-004: a one-tick wait lasts from almost 0 to 166 ms, depending on where the
    /// clock's period stands when it starts.
    /// </summary>
    [Fact]
    public void AOneTickWaitEndsAtTheNextTickWhereverItStarts()
    {
        var step = new TickedPresentation();
        step.Start(TickedPresentationKind.KeyFace, Rectangle.Empty, null, null, Ms(165));
        Assert.True(step.TryFinish(Ms(166), out _));

        step.Start(TickedPresentationKind.KeyFace, Rectangle.Empty, null, null, Ms(166));
        Assert.False(step.TryFinish(Ms(331), out _));
        Assert.True(step.TryFinish(Ms(332), out _));
    }

    [Fact]
    public void FlashesFollowTheCopiesTheirFindingsRecord()
    {
        // FND-UI-017: lit, wait, normal, lit, wait, normal; the normal cell between the two lit
        // copies is replaced without a wait.
        Assert.Equal(new[] { true, true },
            TickedPresentation.LitPattern(TickedPresentationKind.CityCellFlash));
        // FND-UI-018: four copies separated by three waits.
        Assert.Equal(new[] { true, false, true },
            TickedPresentation.LitPattern(TickedPresentationKind.SiteFlash));
        Assert.Equal(new[] { true, false, true },
            TickedPresentation.LitPattern(TickedPresentationKind.SectorDisplayCellFlash));

        var step = new TickedPresentation();
        step.Start(TickedPresentationKind.SiteFlash, new Rectangle(83, 226, 120, 64), null, null, Ms(0));
        Assert.True(step.Lit(Ms(100)));
        Assert.False(step.Lit(Ms(200)));
        Assert.True(step.Lit(Ms(400)));
        Assert.False(step.TryFinish(Ms(497), out _));
        Assert.True(step.TryFinish(Ms(498), out var then));
        Assert.Null(then);
    }

    [Fact]
    public void ClearingAStepDropsItsAction()
    {
        var step = new TickedPresentation();
        step.Start(TickedPresentationKind.KeyFace, Rectangle.Empty, null, () => Assert.Fail("ran"), Ms(0));

        step.Clear();

        Assert.False(step.Active);
        Assert.False(step.TryFinish(Ms(1000), out _));
    }

    /// <summary>FND-UI-019: the pressed images fn_00418CCC copies from PX00129.</summary>
    [Fact]
    public void PressedFacesComeFromTheSheetCellsTheFindingRecords()
    {
        Assert.Equal(new Rectangle(0, 386, 50, 23), PressedKeyFaces.Source(PressedKeyFace.Confirm));
        Assert.Equal(new Rectangle(0, 409, 50, 23), PressedKeyFaces.Source(PressedKeyFace.Cancel));
        Assert.Equal(new Rectangle(120, 205, 32, 63), PressedKeyFaces.Source(PressedKeyFace.SectorBack));
        // FND-UI-015: the sector view's back control at (4,394)-(36,457).
        Assert.Equal(new Rectangle(4, 394, 32, 63),
            PressedKeyFaces.Destination(PressedKeyFace.SectorBack, new Point(4, 394)));
    }
}
