using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class GangDefinitionInformationLayout
{
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
    public static int LeftValueRight => 300;
    public static int RightValueRight => 396;
    public static int ForceY => 216;
    public static int TechLevelY => 225;
    public static int StatisticY(int row) => GangInformationLayout.StatisticY(row);
}
