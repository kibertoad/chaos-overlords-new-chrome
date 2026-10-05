using System.Text;
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
    /// The cells to draw. <paramref name="OffStripGlyph"/> is set when the leading quotient lies off
    /// the font strip: <paramref name="Digits"/> then starts with a blank placeholder, and the first
    /// cell is the raw <c>PX00129</c> cell <see cref="AtlasCell"/> gives for that glyph.
    /// </summary>
    public readonly record struct Value(string Digits, bool IsNegative, bool IsDim, int? OffStripGlyph = null);

    /// <summary>
    /// RULE-UI-004: glyph <c>g</c> is copied from <c>(x, 0, 6, 7)</c> of <c>PX00129</c>, or from
    /// <c>(x, 8, 6, 7)</c> for a negative value, where <c>x</c> is <c>6g</c> cut to a signed 16-bit
    /// number (FND-UI-045), so a large enough glyph wraps back into the bitmap. Null when the cell
    /// does not lie wholly inside the bitmap.
    /// </summary>
    public static Rectangle? AtlasCell(int glyph, bool negative, int atlasWidth)
    {
        var left = (int)unchecked((short)(glyph * OriginalFontLayout.CellWidth));
        // PLACEHOLDER: RULE-UI-004 - what the GDI copy draws for a source cell that is not wholly
        // inside the 512-pixel bitmap is not recorded (FND-UI-045); the cell is left blank.
        return left < 0 || left + OriginalFontLayout.CellWidth > atlasWidth
            ? null
            : new Rectangle(left, negative ? 8 : 0, OriginalFontLayout.CellWidth,
                OriginalFontLayout.GlyphHeight);
    }

    /// <summary>
    /// RULE-UI-004 <c>number_cells</c>: the cells a value fills, right-aligned, leaving out the blank
    /// cells before the first one drawn. A value wider than its cells puts its whole leading
    /// quotient in the first cell, drawn as the strip glyph after the digits, so 123 in two cells
    /// reads <c>&lt;3</c>. A quotient off the strip is returned in <see cref="Value.OffStripGlyph"/>
    /// instead of as a character. The arithmetic is the original's 32-bit arithmetic, so
    /// <see cref="int.MinValue"/> stays negative when negated and its quotients select glyphs below
    /// the digits (FND-UI-045). <paramref name="leadingZeros"/> is the helper's leading-zero flag,
    /// which draws the cells before the first nonzero quotient as <c>0</c> instead of leaving them
    /// out.
    /// </summary>
    public static Value Format(int value, Kind kind = Kind.Modifier, int width = 2, bool leadingZeros = false)
    {
        if (width is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(width), "The recovered helpers support one to five glyph cells.");
        var remainder = value < 0 ? unchecked(-value) : value;
        var divisor = 1;
        for (var cell = 1; cell < width; cell++) divisor *= 10;
        var digits = new StringBuilder(width);
        int? offStrip = null;
        var started = leadingZeros;
        for (var cell = 0; cell < width; cell++)
        {
            var quotient = remainder / divisor;
            remainder -= divisor * quotient;
            if (quotient != 0 || divisor == 1) started = true;
            if (started)
            {
                // Glyph 16 + q is the character '0' + q while it lies on the font strip.
                var glyph = unchecked(quotient + ('0' - OriginalFontLayout.FirstCharacter));
                if (glyph < 0 || quotient > MaximumLeadingQuotient)
                {
                    offStrip = glyph;
                    digits.Append(' ');
                }
                else
                {
                    digits.Append((char)('0' + quotient));
                }
            }
            divisor /= 10;
        }
        return new Value(digits.ToString(), value < 0, kind == Kind.Modifier && value == 0, offStrip);
    }
}
