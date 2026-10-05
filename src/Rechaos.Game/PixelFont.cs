using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

/// <summary>Draws text with the original PX00129 glyph artwork.</summary>
public sealed class PixelFont
{
    private readonly Texture2D _glyphMask;
    private readonly Texture2D _uiAtlas;

    public PixelFont(GraphicsDevice graphicsDevice, Texture2D uiAtlas)
    {
        _uiAtlas = uiAtlas;
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

    /// <summary>
    /// RULE-UI-004: draws formatted cells from <paramref name="position"/>, the left edge of the
    /// first drawn cell. A leading quotient off the font strip is the raw <c>PX00129</c> cell the
    /// original copies (FND-UI-045), or a blank cell when that cell is not wholly inside the bitmap.
    /// </summary>
    public void DrawNumber(SpriteBatch batch, NativeTwoCellNumberPresentation.Value display,
        Vector2 position, Color color)
    {
        Draw(batch, display.Digits, position, color, 1);
        if (display.OffStripGlyph is { } glyph
            && NativeTwoCellNumberPresentation.AtlasCell(glyph, display.IsNegative, _uiAtlas.Width) is { } source)
            batch.Draw(_uiAtlas, new Rectangle((int)position.X, (int)position.Y, source.Width, source.Height),
                source, Color.White);
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
