using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class GangDefinitionInformationLayout
{
    public const int DescriptionColumns = 30;
    public static Rectangle Panel => new(128, 124, 320, 209);
    public static Rectangle BackgroundSource => new(0, 0, 320, 209);
    public static Rectangle Portrait => new(154, 141, 64, 64);
    public static Rectangle Ok => new(161, 293, 49, 22);
    public static int NameLeft => 228;
    public static int DescriptionLeft => 228;
    public static int NameY => 151;
    public static int DescriptionY(int row) => row switch
    {
        >= 0 and < 3 => 169 + row * OriginalFontLayout.LineHeight,
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };
    public static int LeftValueLeft => 300;
    public static int RightValueLeft => 396;
    public static int ForceY => 216;
    public static int TechLevelY => 225;
    public static int StatisticY(int row) => GangInformationLayout.StatisticY(row);

    /// <summary>
    /// Gang definitions store three authored, fixed-width description records.
    /// Keeping those boundaries prevents text from spilling into the right bezel.
    /// </summary>
    public static IReadOnlyList<string> DescriptionLines(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        var lines = new string[3];
        for (var row = 0; row < lines.Length; row++)
        {
            var offset = row * DescriptionColumns;
            if (offset >= description.Length) continue;
            lines[row] = description.Substring(offset,
                Math.Min(DescriptionColumns, description.Length - offset)).TrimEnd();
        }
        return lines;
    }
}
