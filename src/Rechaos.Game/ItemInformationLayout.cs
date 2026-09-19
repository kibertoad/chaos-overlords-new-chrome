using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class ItemInformationLayout
{
    // Native handler 0x0044b699 copies PX05001's 320-pixel alternate crop.
    public static Rectangle Panel => new(128, 124, 320, 209);
    public static Rectangle BackgroundSource => new(0, 0, 320, 209);
    public static Rectangle Portrait => new(162, 141, 48, 48);
    public static Rectangle CompactPortrait => new(176, 155, 20, 20);
    public static Rectangle Ok => new(161, 293, 49, 22);
    public const int DescriptionColumns = 30;
    public const int NameLeft = 228;
    public const int HeaderY = 151;
    public const int TypeRight = 408;
    public const int DescriptionLeft = 228;
    public const int DescriptionY = 169;
    public const int LeftValueLeft = 300;
    public const int RightValueLeft = 396;
    public static int StatisticY(int row) => GangInformationLayout.StatisticY(row);

    /// <summary>
    /// The original item table stores three fixed 30-byte description fields.
    /// Preserve those authored row boundaries instead of reflowing the words.
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

    public static string TypeLabel(int itemType) => itemType switch
    {
        0 => "STRENGTH",
        1 => "BLADE",
        2 => "RANGE",
        3 => "ARMOR",
        4 => "MISC",
        _ => throw new ArgumentOutOfRangeException(nameof(itemType))
    };
}
