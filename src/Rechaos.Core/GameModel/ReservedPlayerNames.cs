namespace Rechaos.Core.GameModel;

/// <summary>
/// The player names the original game treats as cheat codes rather than as names.
/// </summary>
/// <remarks>
/// <para>
/// In the original, naming your overlord <c>SMGFUNDAGE</c> granted the maximum starting cash and
/// <c>SMGISLANDS</c> changed how the city was generated. Both are faithfully recovered in
/// <c>OriginalSetupNameRules</c>, and both are harmless in a hot-seat match: the player typing the
/// name is the only one affected by it, and they chose to.
/// </para>
/// <para>
/// Online they are neither harmless nor a cheat the player is entitled to. A name arrives from the
/// server's roster, every client reads the same one, and the rules fire on all of them — so one
/// player can take the cash bonus with every opponent's client agreeing that they earned it, and
/// the islands name is read with <c>Any</c> over the whole roster, which means one player rewrites
/// the map for everybody. Neither shows up as a desync, because nothing about it is inconsistent.
/// </para>
/// <para>
/// This exists so the code that turns a roster into a match can say "that is not a name". It is the
/// list itself, with no opinion about what to do with it: the lobby refuses one, and the online
/// bootstrap substitutes a seat name it derives, so a server that let one through still plays a fair
/// match.
/// </para>
/// </remarks>
public static class ReservedPlayerNames
{
    /// <summary>Every name the setup rules read as an instruction.</summary>
    public static IReadOnlyList<string> All { get; } =
        [OriginalSetupNameRules.MaximumStartingCashName, OriginalSetupNameRules.IslandsName];

    /// <summary>
    /// Whether <paramref name="name"/> would be read as a cheat rather than as a name.
    /// </summary>
    /// <remarks>
    /// Compared case-insensitively and with surrounding space trimmed, which is wider than the rules
    /// themselves: they match ordinally, so <c>smgfundage</c> does nothing today. Being wider is
    /// deliberate — this guards a lobby, where the cost of refusing a name that only looks like a
    /// cheat is that one player picks another, and the cost of letting one through is an unfair
    /// match. A change of case in the rules must not quietly open the door again.
    /// </remarks>
    public static bool IsReserved(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        foreach (var reserved in All)
        {
            if (string.Equals(trimmed, reserved, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
