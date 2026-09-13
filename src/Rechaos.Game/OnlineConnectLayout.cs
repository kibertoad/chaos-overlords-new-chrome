using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class OnlineConnectLayout
{
    public static Rectangle Central => new(120, 108, 190, 26);
    public static Rectangle Custom => new(330, 108, 190, 26);
    public static Rectangle Server => new(120, 150, 400, 22);
    public static Rectangle Name => new(120, 188, 400, 22);
    public static Rectangle JoinCode => new(120, 226, 400, 22);
    public static Rectangle Password => new(120, 264, 400, 22);
    public static Rectangle Host => new(120, 304, 124, 30);
    public static Rectangle Join => new(258, 304, 124, 30);
    public static Rectangle Back => new(396, 304, 124, 30);
    public const int ServerStatusY = 352;
    public const int StatusY = 374;

    public static IReadOnlyList<Rectangle> Fields => [Server, Name, JoinCode, Password];
}
