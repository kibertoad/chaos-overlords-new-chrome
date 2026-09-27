using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class OriginalPatternMask
{
    public const int Half = 143;
    public const int Sparse = 146;
    public const int Dense = 147;

    // Top-down rows recovered from the executable's 8x8 monochrome bitmap
    // resources. A set bit preserves the destination; a clear bit copies source.
    public static bool PreservesDestination(int resourceId, int x, int y)
    {
        if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
        var row = resourceId switch
        {
            Half => (y & 1) == 0 ? 0x55 : 0xaa,
            Sparse => (y & 1) == 0 ? 0x88 : 0x22,
            Dense => (y & 1) == 0 ? 0xdd : 0x77,
            _ => throw new ArgumentOutOfRangeException(nameof(resourceId))
        };
        return (row & (0x80 >> (x & 7))) != 0;
    }

    /// <summary>
    /// The bitmap <c>fn_00449B20</c> selects for a 16-bit grey passed through
    /// <c>fn_00425E99</c>, which keeps its high byte: up to 85 gives 147, 86 to 170 gives 143
    /// and above gives 146 (FND-GFX-006). 0x7FFF gives 143 and 48,000 gives 146.
    /// </summary>
    public static int ForGrey(int grey16)
    {
        if (grey16 is < 0 or > 0xffff) throw new ArgumentOutOfRangeException(nameof(grey16));
        var shade = grey16 >> 8;
        return shade <= 0x55 ? Dense : shade <= 0xaa ? Half : Sparse;
    }

    /// <summary>
    /// The pixels <c>fn_004266A6</c> puts over a <paramref name="width"/> by
    /// <paramref name="height"/> rectangle in its pattern mode (FND-GFX-006): the pattern's
    /// first row and column sit at the rectangle's top-left corner, a set bit leaves the
    /// destination (transparent here) and a clear bit takes the fill. The fill is drawn with
    /// <c>Rectangle</c>, whose one-pixel border takes <paramref name="outline"/>, the pen of the
    /// scratch surface, and whose inside takes <paramref name="fill"/>.
    /// </summary>
    public static Color[] ShadedRectangle(int resourceId, int width, int height, Color fill, Color outline)
    {
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (PreservesDestination(resourceId, x, y))
            {
                pixels[y * width + x] = Color.Transparent;
                continue;
            }
            var border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
            pixels[y * width + x] = border ? outline : fill;
        }
        return pixels;
    }
}
