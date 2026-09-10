namespace Rechaos.Core.GameModel;

/// <summary>Transient fresh-game name modifiers recovered from the original setup path.</summary>
internal static class OriginalSetupNameRules
{
    private const string MaximumStartingCashName = "SMGFUNDAGE";

    public const int MaximumStartingCash = 1_500;

    public static int ApplyStartingCash(string playerName, int ordinaryStartingCash)
    {
        ArgumentNullException.ThrowIfNull(playerName);
        return StringComparer.Ordinal.Equals(playerName, MaximumStartingCashName)
            ? MaximumStartingCash
            : ordinaryStartingCash;
    }
}
