namespace Rechaos.Core.GameModel;

/// <summary>Persistent hire modifiers recovered from the original fresh-game name scan.</summary>
internal static class OriginalHireCheatRules
{
    private const string MaximumForceName = "SMGMILK";

    public static bool UsesMaximumHireForce(MatchPlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return StringComparer.Ordinal.Equals(player.Setup.Name, MaximumForceName);
    }
}
