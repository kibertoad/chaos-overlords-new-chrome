using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed class PxDimensionsTests
{
    [Theory]
    [InlineData(76_526, 158)]
    [InlineData(76_042, 157)]
    public void Px06HeightMatchesExactSixteenBitPayloadSize(long bytes, int expectedHeight)
    {
        Assert.True(PxDimensions.TryGet("PX06001", bytes, out var size));
        Assert.Equal(242, size.Width);
        Assert.Equal(expectedHeight, size.Height);
        Assert.Equal(bytes, 54 + size.Width * size.Height * 2);
    }
}
