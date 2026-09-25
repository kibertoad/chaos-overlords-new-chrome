using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalPatternMaskTests
{
    [Fact]
    public void MasksMatchEmbeddedMonochromeResources()
    {
        Assert.Equal(32, CountPreserved(OriginalPatternMask.Half));
        Assert.Equal(16, CountPreserved(OriginalPatternMask.Sparse));
        Assert.Equal(48, CountPreserved(OriginalPatternMask.Dense));
        Assert.True(OriginalPatternMask.PreservesDestination(OriginalPatternMask.Sparse, 0, 0));
        Assert.True(OriginalPatternMask.PreservesDestination(OriginalPatternMask.Sparse, 2, 1));
        Assert.False(OriginalPatternMask.PreservesDestination(OriginalPatternMask.Sparse, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OriginalPatternMask.PreservesDestination(999, 0, 0));
    }

    private static int CountPreserved(int resourceId) =>
        Enumerable.Range(0, 8).Sum(y =>
            Enumerable.Range(0, 8).Count(x =>
                OriginalPatternMask.PreservesDestination(resourceId, x, y)));

    /// <summary>
    /// FND-GFX-006: fn_00425E99 keeps the high byte of each 16-bit grey and fn_00449B20 picks
    /// 147 up to 85, 143 from 86 to 170 and 146 above.
    /// </summary>
    [Fact]
    public void AGreySelectsItsPatternByItsHighByte()
    {
        Assert.Equal(OriginalPatternMask.Half, OriginalPatternMask.ForGrey(0x7fff));
        Assert.Equal(OriginalPatternMask.Sparse, OriginalPatternMask.ForGrey(48000));
        Assert.Equal(OriginalPatternMask.Dense, OriginalPatternMask.ForGrey(0x4000));
        Assert.Equal(OriginalPatternMask.Dense, OriginalPatternMask.ForGrey(0x55ff));
        Assert.Equal(OriginalPatternMask.Half, OriginalPatternMask.ForGrey(0x5600));
        Assert.Equal(OriginalPatternMask.Half, OriginalPatternMask.ForGrey(0xaaff));
        Assert.Equal(OriginalPatternMask.Sparse, OriginalPatternMask.ForGrey(0xab00));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalPatternMask.ForGrey(0x10000));
    }

    /// <summary>
    /// FND-GFX-006: the pattern starts at the rectangle's corner, a set bit keeps the
    /// destination, and the border takes the pen while the inside takes the brush.
    /// </summary>
    [Fact]
    public void AShadedRectangleAnchorsThePatternAtItsCornerAndOutlinesWithThePen()
    {
        var pixels = OriginalPatternMask.ShadedRectangle(
            OriginalPatternMask.Half, 4, 4, Color.White, Color.Black);
        Assert.Equal(
        [
            Color.Black, Color.Transparent, Color.Black, Color.Transparent,
            Color.Transparent, Color.White, Color.Transparent, Color.Black,
            Color.Black, Color.Transparent, Color.White, Color.Transparent,
            Color.Transparent, Color.Black, Color.Transparent, Color.Black
        ], pixels);
    }
}
