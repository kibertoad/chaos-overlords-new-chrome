using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class HitTest
{
    /// <summary>
    /// The first index below <paramref name="count"/> whose bounds contain the point, or -1.
    /// </summary>
    public static int IndexAt(int count, Func<int, Rectangle> bounds, Point point)
    {
        for (var index = 0; index < count; index++)
        {
            if (bounds(index).Contains(point)) return index;
        }
        return -1;
    }
}
