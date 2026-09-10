namespace Rechaos.Core.GameModel;

/// <summary>
/// Action gates recovered from family 11 at 0x00420950.
/// </summary>
internal static class OriginalAiFamilyElevenRules
{
    public const int HealForceLimit = 8;
    public const int FormationSize = 6;

    public static bool ShouldHeal(
        int force,
        int effectiveHeal,
        GangAction previousAction) =>
        force < HealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal
        && previousAction != GangAction.Attack;

    public static int FormationLeaderSlot(
        IReadOnlyList<int> families,
        int gangSlot)
    {
        ArgumentNullException.ThrowIfNull(families);
        if (families.Count != AiPlanningState.GangSlotsPerPlayer)
            throw new ArgumentException(
                "Formation lookup requires all 81 original gang slots.",
                nameof(families));
        if (gangSlot is < 0 or >= AiPlanningState.GangSlotsPerPlayer)
            throw new ArgumentOutOfRangeException(nameof(gangSlot));
        if (families[gangSlot] != 11)
            throw new ArgumentException("Formation lookup requires a family-11 gang slot.", nameof(gangSlot));

        var ordinal = -1;
        var leaderSlot = -1;
        for (var slot = 0; slot <= gangSlot; slot++)
        {
            if (families[slot] != 11) continue;
            ordinal++;
            if (ordinal % FormationSize == 0) leaderSlot = slot;
        }
        return leaderSlot;
    }
}
