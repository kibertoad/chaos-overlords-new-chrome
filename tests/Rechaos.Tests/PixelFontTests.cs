using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class PixelFontTests
{
    [Fact]
    public void OriginalGlyphStripCoversPrintableUiCharactersThroughZ()
    {
        Assert.Equal(new Rectangle(0, 0, 354, 7), OriginalFontLayout.AtlasBounds);
        Assert.True(OriginalFontLayout.TryGlyph(' ', out var space));
        Assert.Equal(new Rectangle(0, 0, 6, 7), space);
        Assert.True(OriginalFontLayout.TryGlyph('$', out var dollar));
        Assert.Equal(new Rectangle(24, 0, 6, 7), dollar);
        Assert.True(OriginalFontLayout.TryGlyph('Z', out var zed));
        Assert.Equal(new Rectangle(348, 0, 6, 7), zed);
        Assert.True(OriginalFontLayout.TryGlyph('a', out var lowerA));
        Assert.Equal(new Rectangle(198, 0, 6, 7), lowerA);
        Assert.False(OriginalFontLayout.TryGlyph('_', out _));
    }

    [Fact]
    public void SupplementalBracketsFollowTheOriginalStripInTheGlyphMask()
    {
        Assert.Equal(366, OriginalFontLayout.MaskWidth);
        Assert.True(OriginalFontLayout.TryGlyph('[', out var open));
        Assert.Equal(new Rectangle(354, 0, 6, 7), open);
        Assert.True(OriginalFontLayout.TryGlyph(']', out var close));
        Assert.Equal(new Rectangle(360, 0, 6, 7), close);
        Assert.True(SupplementalFontGlyphs.IsLit(0, 1, 3));
        Assert.True(SupplementalFontGlyphs.IsLit(1, 3, 3));
    }
}
