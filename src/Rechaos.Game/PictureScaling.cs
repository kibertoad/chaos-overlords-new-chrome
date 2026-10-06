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

    /// <summary>
    /// The <paramref name="size"/> picture scaled from <paramref name="source"/> of a sheet whose
    /// pixels are <paramref name="pixels"/>, <paramref name="stride"/> to a row.
    /// </summary>
    public static Color[] Scale(Color[] pixels, int stride, Rectangle source, Point size)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var scaled = new Color[size.X * size.Y];
        for (var y = 0; y < size.Y; y++)
        {
            var row = (source.Y + SourceIndex(y, source.Height, size.Y)) * stride;
            for (var x = 0; x < size.X; x++)
                scaled[y * size.X + x] = pixels[row + source.X + SourceIndex(x, source.Width, size.X)];
        }
        return scaled;
    }
}

/// <summary>
/// The pictures a panel draws scaled (PictureScaling), each scaled once from its sheet and kept, so
/// a frame draws it with one draw at its own size.
/// </summary>
public sealed class ScaledPictureCache
{
    private readonly Dictionary<(Rectangle Source, Point Size), Texture2D> _pictures = [];
    private Texture2D? _sheet;
    private Color[]? _pixels;

    public void Draw(SpriteBatch batch, Texture2D sheet, Rectangle source, Rectangle destination)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(sheet);
        if (!ReferenceEquals(sheet, _sheet))
        {
            foreach (var picture in _pictures.Values) picture.Dispose();
            _pictures.Clear();
            _sheet = sheet;
            _pixels = null;
        }
        var key = (source, destination.Size);
        if (!_pictures.TryGetValue(key, out var scaled))
        {
            if (_pixels is null)
            {
                _pixels = new Color[sheet.Width * sheet.Height];
                sheet.GetData(_pixels);
            }
            scaled = new Texture2D(batch.GraphicsDevice, destination.Width, destination.Height);
            scaled.SetData(PictureScaling.Scale(_pixels, sheet.Width, source, destination.Size));
            _pictures[key] = scaled;
        }
        batch.Draw(scaled, destination, Color.White);
    }
}
