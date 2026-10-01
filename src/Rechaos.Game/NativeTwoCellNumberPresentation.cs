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

    public readonly record struct Value(string Digits, bool IsNegative, bool IsDim);

    /// <summary>
    /// RULE-UI-004 <c>number_cells</c>: the cells a value fills, right-aligned. A value wider than
    /// its cells puts its whole leading quotient in the first cell, drawn as the strip glyph after
    /// the digits, so 123 in two cells reads <c>&lt;3</c>.
    /// </summary>
    public static Value Format(int value, Kind kind = Kind.Modifier, int width = 2)
    {
        if (width is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(width), "The recovered helpers support one to five glyph cells.");
        var magnitude = Math.Abs((long)value);
        var divisor = (long)Math.Pow(10, width - 1);
        var leading = magnitude / divisor;
        if (leading > MaximumLeadingQuotient)
            throw new ArgumentOutOfRangeException(nameof(value),
                "The leading cell of the value lies past the font strip.");
        var digits = leading < 10
            ? magnitude.ToString()
            : (char)('0' + leading) + (width == 1 ? string.Empty
                : (magnitude % divisor).ToString().PadLeft(width - 1, '0'));
        return new Value(digits, value < 0, kind == Kind.Modifier && value == 0);
    }
}
