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
}
