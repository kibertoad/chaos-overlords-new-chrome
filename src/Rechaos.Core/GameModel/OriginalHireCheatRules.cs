namespace Rechaos.Core.GameModel;

/// <summary>Persistent hire modifiers recovered from the original fresh-game name scan.</summary>
internal static class OriginalHireCheatRules
{
    private const string MaximumForceName = "SMGMILK";

    public static bool DetectMaximumHireForce(string playerName)
    {
        ArgumentNullException.ThrowIfNull(playerName);
        return StringComparer.Ordinal.Equals(playerName, MaximumForceName);
    }
}
