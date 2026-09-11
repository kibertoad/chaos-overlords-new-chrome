using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class OnlineConnectLayout
{
    public static Rectangle Server => new(120, 116, 400, 22);
    public static Rectangle Name => new(120, 154, 400, 22);
    public static Rectangle JoinCode => new(120, 192, 400, 22);
    public static Rectangle Password => new(120, 230, 400, 22);
    public static Rectangle Host => new(120, 270, 124, 30);
    public static Rectangle Join => new(258, 270, 124, 30);
    public static Rectangle Back => new(396, 270, 124, 30);
    public const int StatusY = 326;

    public static IReadOnlyList<Rectangle> Fields => [Server, Name, JoinCode, Password];
}
