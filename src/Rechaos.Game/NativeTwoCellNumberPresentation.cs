namespace Rechaos.Game;

/// <summary>Presentation contract of the original fixed-width numeric helpers.</summary>
public static class NativeTwoCellNumberPresentation
{
    public enum Kind
    {
        Baseline,
        Modifier
    }

    public readonly record struct Value(string Digits, bool IsNegative, bool IsDim);

    public static Value Format(int value, Kind kind = Kind.Modifier, int width = 2)
    {
        if (width is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(width), "The recovered helpers support one to four glyph cells.");
        var maximum = width switch
        {
            1 => 9,
            2 => 99,
            3 => 999,
            4 => 9999,
            _ => throw new ArgumentOutOfRangeException(nameof(width))
        };
        if (value is < -9999 or > 9999 || Math.Abs(value) > maximum)
            throw new ArgumentOutOfRangeException(nameof(value),
                "The value does not fit in the native fixed-width glyph field.");
        return new Value(Math.Abs(value).ToString(), value < 0,
            kind == Kind.Modifier && value == 0);
    }
}
