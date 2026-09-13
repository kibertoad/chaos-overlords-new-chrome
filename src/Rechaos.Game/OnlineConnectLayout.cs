using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class OnlineConnectLayout
{
    public static Rectangle Central => new(120, 108, 190, 26);
    public static Rectangle Custom => new(330, 108, 190, 26);
    public static Rectangle Server => new(120, 150, 400, 22);
    public static Rectangle HostRole => new(120, 188, 190, 26);
    public static Rectangle JoinRole => new(330, 188, 190, 26);
    public static Rectangle Name => new(120, 226, 400, 22);
    public static Rectangle JoinCode => new(120, 264, 400, 22);
    public static Rectangle Password => new(120, 302, 400, 22);
    public static Rectangle Continue => new(120, 340, 190, 30);
    public static Rectangle Back => new(330, 340, 190, 30);
    public const int ServerStatusY = 390;
    public const int StatusY = 410;

    public static IReadOnlyList<Rectangle> Fields => [Server, Name, JoinCode, Password];
}
