using System.Globalization;

namespace Rechaos.Game;

/// <summary>The culture the whole game formats and parses with.</summary>
/// <remarks>
/// The screens are drawn with the original's fixed glyph strip and read like the original, so
/// numbers must come out the same on every machine. Interpolated strings throughout the client
/// format with the current culture; under a culture whose negative sign is U+2212 (sv-SE under ICU,
/// among others) a negative cash balance lost its sign, and a culture with a different digit
/// grouping or decimal separator would change text the layouts measure. Nothing in the client
/// wants the player's culture: dates shown to the player are already formatted with explicit
/// invariant patterns, and every persisted or wire value uses invariant or JSON formatting.
/// </remarks>
public static class GameCulture
{
    /// <summary>Makes the invariant culture current for this thread and the default for new ones.</summary>
    public static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }
}
