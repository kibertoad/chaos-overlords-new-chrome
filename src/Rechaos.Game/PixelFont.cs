using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

/// <summary>Draws text with the original PX00129 glyph artwork.</summary>
public sealed class PixelFont
{
    private readonly Texture2D _glyphMask;

    public PixelFont(GraphicsDevice graphicsDevice, Texture2D uiAtlas)
    {
        var bounds = OriginalFontLayout.AtlasBounds;
        var maskWidth = OriginalFontLayout.MaskWidth;
        var atlas = new Color[uiAtlas.Width * uiAtlas.Height];
        uiAtlas.GetData(atlas);
        var mask = new Color[maskWidth * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        for (var x = 0; x < bounds.Width; x++)
        {
            var source = atlas[(bounds.Y + y) * uiAtlas.Width + bounds.X + x];
            var intensity = Math.Max(source.R, Math.Max(source.G, source.B));
            mask[y * maskWidth + x] = intensity == 0
                ? Color.Transparent
                : new Color(intensity, intensity, intensity, intensity);
        }

        AppendSupplementalGlyphs(mask, maskWidth, bounds.Width);
        _glyphMask = new Texture2D(graphicsDevice, maskWidth, bounds.Height);
        _glyphMask.SetData(mask);
    }

    private static void AppendSupplementalGlyphs(Color[] mask, int maskWidth, int left)
    {
        for (var glyph = 0; glyph < SupplementalFontGlyphs.Characters.Length; glyph++)
        for (var y = 0; y < OriginalFontLayout.GlyphHeight; y++)
        for (var x = 0; x < OriginalFontLayout.CellWidth; x++)
            if (SupplementalFontGlyphs.IsLit(glyph, x, y))
                mask[y * maskWidth + left + glyph * OriginalFontLayout.CellWidth + x] = Color.White;
    }

    public void Draw(SpriteBatch batch, string text, Vector2 position, Color color, int scale)
    {
        var startX = position.X;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                position.X = startX;
                position.Y += OriginalFontLayout.LineHeight * scale;
                continue;
            }

            if (OriginalFontLayout.TryGlyph(character, out var source))
                batch.Draw(_glyphMask,
                    new Rectangle((int)position.X, (int)position.Y,
                        source.Width * scale, source.Height * scale),
                    source, color);
            position.X += OriginalFontLayout.CellWidth * scale;
        }
    }
}
