using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalWhiteKeyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryRgb555ColorPreservesAllButMaximumWhite(bool replicateLowBits)
    {
        // FMT-GFX-001, FND-PLATFORM-008: maximum packed RGB555 white is
        // the key; black and every other packed color remain opaque.
        // Synthetic pixels exercise the full color space without original media.
        var colors = new Color[32768];
        for (var packed = 0; packed < colors.Length; packed++)
            colors[packed] = new Color(Expand((packed >> 10) & 31),
                Expand((packed >> 5) & 31), Expand(packed & 31), (byte)255);
        var original = (Color[])colors.Clone();
        OriginalWhiteKey.Apply(colors);
        for (var packed = 0; packed < colors.Length; packed++)
            Assert.Equal(packed == 0x7fff ? Color.Transparent : original[packed], colors[packed]);

        byte Expand(int channel) => (byte)((channel << 3) | (replicateLowBits ? channel >> 2 : 0));
    }
}
