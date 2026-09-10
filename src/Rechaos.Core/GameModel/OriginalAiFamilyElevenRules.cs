namespace Rechaos.Core.GameModel;

/// <summary>
/// Action gates recovered from family 11 at 0x00420950.
/// </summary>
internal static class OriginalAiFamilyElevenRules
{
    public const int HealForceLimit = 8;

    public static bool ShouldHeal(
        int force,
        int effectiveHeal,
        GangAction previousAction) =>
        force < HealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal
        && previousAction != GangAction.Attack;
}
