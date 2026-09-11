namespace Rechaos.Core.GameModel;

/// <summary>Transient fresh-game name modifiers recovered from the original setup path.</summary>
internal static class OriginalSetupNameRules
{
    // Internal rather than private: ReservedPlayerNames publishes the list so an online lobby can
    // refuse a name the rules below would read as an instruction. One spelling, in one place.
    internal const string MaximumStartingCashName = "SMGFUNDAGE";
    internal const string IslandsName = "SMGISLANDS";
    internal const string ExtraRightHandsName = "SMGSPANK";
    internal const string OmniscienceName = "SMGHUBBLE";
    internal const string AssaultTeamName = "SMGKICKASS";

    public const int MaximumStartingCash = 1_500;
    public const int ExtraStartingGangCount = 5;
    public const short AssaultGangDefinitionId = 59;
    public const short AssaultWeaponItemId = 23;
    public const short AssaultArmorItemId = 37;
    public const short AssaultMiscellaneousItemId = 52;

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

    public static bool EnablesExtraRightHands(string playerName) =>
        Matches(playerName, ExtraRightHandsName);

    public static bool EnablesOmniscience(string playerName) =>
        Matches(playerName, OmniscienceName);

    public static bool EnablesAssaultTeam(string playerName) =>
        Matches(playerName, AssaultTeamName);

    private static bool Matches(string playerName, string modifier)
    {
        ArgumentNullException.ThrowIfNull(playerName);
        return StringComparer.Ordinal.Equals(playerName, modifier);
    }
}
