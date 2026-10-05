using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

/// <summary>
/// EXP-UI-006, EXP-UI-008: the original shrinks a picture by taking, for each destination pixel,
/// the source pixel under its centre: column <c>(2x + 1) * w / (2W)</c> and row
/// <c>(2y + 1) * h / (2H)</c>, rounded down, for a <c>w</c>-by-<c>h</c> source drawn
/// <c>W</c> by <c>H</c>. At half size that is the pixel at <c>(2x + 1, 2y + 1)</c>.
/// </summary>
public static class PictureScaling
{
    public static int SourceIndex(int destination, int sourceLength, int destinationLength)
    {
        if (destinationLength <= 0) throw new ArgumentOutOfRangeException(nameof(destinationLength));
        if (destination < 0 || destination >= destinationLength) throw new ArgumentOutOfRangeException(nameof(destination));
        return (2 * destination + 1) * sourceLength / (2 * destinationLength);
    }

    public static void Draw(SpriteBatch batch, Texture2D texture, Rectangle source, Rectangle destination)
    {
        for (var y = 0; y < destination.Height; y++)
        {
            var row = source.Y + SourceIndex(y, source.Height, destination.Height);
            for (var x = 0; x < destination.Width; x++)
                batch.Draw(texture, new Rectangle(destination.X + x, destination.Y + y, 1, 1),
                    new Rectangle(source.X + SourceIndex(x, source.Width, destination.Width), row, 1, 1),
                    Color.White);
        }
    }
}
