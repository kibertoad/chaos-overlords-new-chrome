using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The compact gang information panel an order panel opens for a gang's portrait (SCR-GANG-001,
/// FND-GANG-010): the alternate 320-pixel crop of PX05022, with the fields of SCR-GANG-002 24
/// pixels further right.
/// </summary>
public static class GangDefinitionInformationLayout
{
    public const int DescriptionClearWidth = 180;
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
    /// SCR-GANG-001, FND-GANG-010: the four areas drawn black through a pattern over the base
    /// values, (282,243)-(294,261), (378,243)-(390,261), (282,270)-(294,315) and
    /// (378,270)-(390,315).
    /// </summary>
    public static IReadOnlyList<Rectangle> BaseValueDimAreas { get; } =
    [
        new(282, 243, 12, 18),
        new(378, 243, 12, 18),
        new(282, 270, 12, 45),
        new(378, 270, 12, 45)
    ];
}
