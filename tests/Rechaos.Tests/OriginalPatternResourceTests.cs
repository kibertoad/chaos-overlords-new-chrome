using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalPatternResourceTests
{
    private const int BitmapResourceType = 2;

    [Fact]
    public void EveryMaskBitMatchesTheOriginalExecutableBitmap()
    {
        // needs: GAME_DIR
        // FND-UI-031, FND-GFX-006: compare all mask bits with the executable's bitmap resources,
        // read as 8-by-8 one-bit DIBs with a black and white palette, stored bottom-up with each
        // row padded to four bytes.
        var bitmaps = ExecutableResources.Read(ExecutableResources.RequireExecutable(), BitmapResourceType)
            .ToLookup(resource => resource.Id, resource => resource.Data);
        foreach (var id in new[] { 143, 146, 147 })
        {
            var dib = Assert.Single(bitmaps[id]);
            Assert.Equal(40, BitConverter.ToInt32(dib, 0));
            Assert.Equal(8, BitConverter.ToInt32(dib, 4));
            Assert.Equal(8, BitConverter.ToInt32(dib, 8));
            Assert.Equal(1, BitConverter.ToInt16(dib, 12));
            Assert.Equal(1, BitConverter.ToInt16(dib, 14));
            Assert.Equal(0, BitConverter.ToInt32(dib, 16));
            Assert.Equal(0, BitConverter.ToInt32(dib, 32));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0 }, dib[40..48]);
            Assert.Equal(48 + 8 * 4, dib.Length);
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
                Assert.Equal((dib[48 + (7 - y) * 4] & (0x80 >> x)) != 0,
                    OriginalPatternMask.PreservesDestination(id, x, y));
        }
    }
}
