namespace Rechaos.Core.GameModel;

/// <summary>Transient fresh-game name modifiers recovered from the original setup path.</summary>
internal static class OriginalSetupNameRules
{
    // Internal rather than private: ReservedPlayerNames publishes the list so an online lobby can
    // refuse a name the rules below would read as an instruction. One spelling, in one place.
    internal const string MaximumStartingCashName = "SMGFUNDAGE";
    internal const string IslandsName = "SMGISLANDS";

    public const int MaximumStartingCash = 1_500;

    public static int ApplyStartingCash(string playerName, int ordinaryStartingCash)
    {
        ArgumentNullException.ThrowIfNull(playerName);
        return StringComparer.Ordinal.Equals(playerName, MaximumStartingCashName)
            ? MaximumStartingCash
            : ordinaryStartingCash;
    }

    public static bool EnablesIslands(string playerName)
    {
        ArgumentNullException.ThrowIfNull(playerName);
        return StringComparer.Ordinal.Equals(playerName, IslandsName);
    }
}
