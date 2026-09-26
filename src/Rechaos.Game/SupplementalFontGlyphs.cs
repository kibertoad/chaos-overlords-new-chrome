namespace Rechaos.Game;

/// <summary>
/// Glyphs the original PX00129 font strip lacks, drawn in its six-by-seven cell so they sit
/// alongside the original artwork. <see cref="PixelFont"/> appends them after the strip.
/// </summary>
public static class SupplementalFontGlyphs
{
    public const string Characters = "[]";

    private static readonly string[][] Rows =
    [
        [
            ".###..",
            ".#....",
            ".#....",
            ".#....",
            ".#....",
            ".#....",
            ".###.."
        ],
        [
            ".###..",
            "...#..",
            "...#..",
            "...#..",
            "...#..",
            "...#..",
            ".###.."
        ]
    ];

    /// <summary>Whether pixel (<paramref name="x"/>, <paramref name="y"/>) of the glyph at <paramref name="index"/> is lit.</summary>
    public static bool IsLit(int index, int x, int y) => Rows[index][y][x] == '#';
}
