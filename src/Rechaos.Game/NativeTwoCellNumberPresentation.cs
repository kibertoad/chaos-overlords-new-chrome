namespace Rechaos.Game;

/// <summary>Presentation contract of the original fixed-width numeric helper.</summary>
public static class NativeTwoCellNumberPresentation
{
    public enum Kind
    {
        Baseline,
        Modifier
    }

    public readonly record struct Value(string Digits, bool IsNegative, bool IsDim);

    public static Value Format(int value, Kind kind = Kind.Modifier)
    {
        if (value is < -99 or > 99)
            throw new ArgumentOutOfRangeException(nameof(value),
                "The native numeric helper has exactly two glyph cells.");
        return new Value(Math.Abs(value).ToString(), value < 0,
            kind == Kind.Modifier && value == 0);
    }
}
