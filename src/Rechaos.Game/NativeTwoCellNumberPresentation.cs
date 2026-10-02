using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>Presentation contract of the original fixed-width numeric helpers.</summary>
public static class NativeTwoCellNumberPresentation
{
    /// <summary>
    /// The largest quotient the first cell can show from the font strip: glyph <c>16 + q</c> is the
    /// character <c>'0' + q</c>, and the strip ends at <see cref="OriginalFontLayout.LastCharacter"/>.
    /// </summary>
    private const int MaximumLeadingQuotient = OriginalFontLayout.LastCharacter - '0';

    public enum Kind
    {
        Baseline,
        Modifier
    }

    /// <summary>
    /// The cells to draw. <paramref name="OffStripGlyph"/> is set when the leading quotient lies past
    /// the font strip: <paramref name="Digits"/> then starts with a blank placeholder, and the first
    /// cell is the raw <c>PX00129</c> cell <see cref="AtlasCell"/> gives for that glyph.
    /// </summary>
    public readonly record struct Value(string Digits, bool IsNegative, bool IsDim, int? OffStripGlyph = null);

    /// <summary>
    /// RULE-UI-004: glyph <c>g</c> is copied from <c>(6g, 0, 6, 7)</c> of <c>PX00129</c>, or from
    /// <c>(6g, 8, 6, 7)</c> for a negative value, wherever that lands in the bitmap. Null when the cell
    /// lies past the bitmap's right edge, where the original's output is not recorded.
    /// </summary>
    public static Rectangle? AtlasCell(int glyph, bool negative, int atlasWidth)
    {
        var left = (long)glyph * OriginalFontLayout.CellWidth;
        return left + OriginalFontLayout.CellWidth > atlasWidth
            ? null
            : new Rectangle((int)left, negative ? 8 : 0, OriginalFontLayout.CellWidth,
                OriginalFontLayout.GlyphHeight);
    }

    /// <summary>
    /// RULE-UI-004 <c>number_cells</c>: the cells a value fills, right-aligned. A value wider than
    /// its cells puts its whole leading quotient in the first cell, drawn as the strip glyph after
    /// the digits, so 123 in two cells reads <c>&lt;3</c>. A quotient past the strip's last character
    /// is returned in <see cref="Value.OffStripGlyph"/> instead of as a character.
    /// </summary>
    public static Value Format(int value, Kind kind = Kind.Modifier, int width = 2)
    {
        if (width is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(width), "The recovered helpers support one to five glyph cells.");
        var magnitude = Math.Abs((long)value);
        var divisor = (long)Math.Pow(10, width - 1);
        var leading = magnitude / divisor;
        var offStrip = leading > MaximumLeadingQuotient;
        var digits = leading < 10
            ? magnitude.ToString()
            : (offStrip ? ' ' : (char)('0' + leading)) + (width == 1 ? string.Empty
                : (magnitude % divisor).ToString().PadLeft(width - 1, '0'));
        return new Value(digits, value < 0, kind == Kind.Modifier && value == 0,
            offStrip ? (int)Math.Min(int.MaxValue, '0' - OriginalFontLayout.FirstCharacter + leading) : null);
    }
}
