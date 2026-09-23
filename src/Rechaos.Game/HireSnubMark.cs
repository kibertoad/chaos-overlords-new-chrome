using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The red cross drawn over a snubbed hire offer, generated rather than read from an atlas.
/// </summary>
/// <remarks>
/// The original marks a Reject selection with a red cross over the offer's portrait, but its
/// <c>PX00129</c> source rectangle has not been recovered yet. Until it is, the dock composites
/// this generated cross in the same aperture as the <c>HIRED</c> stamp; swapping in the native
/// sprite only replaces the texture and source the dock draws.
/// </remarks>
public static class HireSnubMark
{
    public const int Size = 60;
    private const int Margin = 6;
    private const int CoreHalfWidth = 3;
    private const int OutlineHalfWidth = CoreHalfWidth + 1;

    public static readonly Color Core = new(224, 24, 24);
    public static readonly Color Outline = new(72, 0, 0);

    /// <summary>Row-major <see cref="Size"/>-square pixels, transparent outside the cross.</summary>
    public static Color[] Pixels()
    {
        var pixels = new Color[Size * Size];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                pixels[y * Size + x] = PixelAt(x, y);
        return pixels;
    }

    public static Color PixelAt(int x, int y)
    {
        if (x is < Margin or >= Size - Margin || y is < Margin or >= Size - Margin)
            return Color.Transparent;
        // Distance, along one axis, from each diagonal of the square.
        var distance = Math.Min(Math.Abs(x - y), Math.Abs(x + y - (Size - 1)));
        return distance <= CoreHalfWidth ? Core
            : distance <= OutlineHalfWidth ? Outline
            : Color.Transparent;
    }
}
