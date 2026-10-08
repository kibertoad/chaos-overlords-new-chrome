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
    /// original copies (FND-UI-065), cut to the part of it inside the bitmap (EXP-UI-002).
    /// </summary>
    public void DrawNumber(SpriteBatch batch, NativeTwoCellNumberPresentation.Value display,
        Vector2 position, Color color)
    {
        if (display.IsDim)
        {
            // The dim 0 is a cell of the art of its own, not the digit in another colour
            // (EXP-UI-006).
            var cell = NativeTwoCellNumberPresentation.DimZeroCell;
            batch.Draw(_uiAtlas, new Rectangle((int)position.X, (int)position.Y, cell.Width, cell.Height),
                cell, Color.White);
            return;
        }
        Draw(batch, display.Digits, position, color, 1);
        if (display.OffStripGlyph is { } glyph
            && NativeTwoCellNumberPresentation.AtlasCell(glyph, display.IsNegative, _uiAtlas.Width) is { } copy)
            batch.Draw(_uiAtlas, copy.Destination((int)position.X, (int)position.Y), copy.Source, Color.White);
    }

    /// <summary>
    /// Copies each character's cell of the PX00129 strip whose top-left corner is
    /// <paramref name="strip"/>, as the original's text draw does, from <paramref name="position"/>.
    /// Characters outside the strip leave their cell untouched.
    /// </summary>
    public void Copy(SpriteBatch batch, string text, Point position, Point strip)
    {
        foreach (var character in text)
        {
            if (OriginalFontLayout.TryGlyph(character, out var glyph)
                && glyph.X < OriginalFontLayout.AtlasBounds.Width)
                batch.Draw(_uiAtlas,
                    new Rectangle(position.X, position.Y, glyph.Width, glyph.Height),
                    new Rectangle(strip.X + glyph.X, strip.Y + glyph.Y, glyph.Width, glyph.Height), Color.White);
            position.X += OriginalFontLayout.CellWidth;
        }
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
