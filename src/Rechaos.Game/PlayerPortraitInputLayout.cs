using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static partial class PlayerPortraitLayout
{
    // The native local-setup handler uses a 64-by-68 interaction cell whose
    // origin is five pixels below the drawn portrait. Its broad side bands and
    // ten-pixel bottom band deliberately exceed the visible arrow/name glyphs.
    public static Rectangle SetupHit(int player) =>
        Player(player, 397, 94, 83, 64, 68, rowStride: 74);

    public static Rectangle PreviousHit(int player) =>
        Player(player, 397, 94, 83, 16, 58, rowStride: 74);

    public static Rectangle NextHit(int player) =>
        Player(player, 446, 94, 83, 15, 58, rowStride: 74);

    public static Rectangle NameHit(int player) =>
        Player(player, 397, 152, 83, 64, 10, rowStride: 74);

    public static bool SetupDragMoved(Point pressed, Point current) =>
        current.X < pressed.X - 2 || current.X >= pressed.X + 2
        || current.Y < pressed.Y - 2 || current.Y >= pressed.Y + 2;

    public static Point ClampSetupDragPoint(Point point) => new(
        Math.Clamp(point.X, 20, 620), Math.Clamp(point.Y, 20, 440));

    public static Rectangle SetupDragToken(Point point)
    {
        var center = ClampSetupDragPoint(point);
        return new Rectangle(center.X - 20, center.Y - 20, 40, 40);
    }
}
