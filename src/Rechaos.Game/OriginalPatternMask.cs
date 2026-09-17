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
}
