namespace Rechaos.Core.GameModel;

/// <summary>
/// Exact terminal boundaries recovered from family handler 1 at 0x00434080.
/// The surrounding target enumeration is intentionally kept outside this
/// kernel until its remaining selectors are bounded.
/// </summary>
internal static class OriginalAiFamilyOneRules
{
    private const int MinimumRawSectorOwner = -3;
    public const int StrictHealForceLimit = 8;
    public const int CommonHealForceLimit = 9;
    public const int MinimumEffectiveHeal = -3;
    public const int CrimeCashThreshold = 50;
    public const int StrictCashThreshold = 50;
    public const int ChaosToleranceThreshold = 4;

    public static bool CanHeal(int force, int effectiveHeal, int forceLimitExclusive)
    {
        if (forceLimitExclusive is not (StrictHealForceLimit or CommonHealForceLimit))
            throw new ArgumentOutOfRangeException(nameof(forceLimitExclusive));
        return force < forceLimitExclusive && effectiveHeal >= MinimumEffectiveHeal;
    }

    public static GangAction SelectStrictCashContinuation(int cash) =>
        cash > StrictCashThreshold ? GangAction.Snitch : GangAction.Move;

    public static GangAction SelectCrimeOrMove(int cash, int tolerance)
    {
        if (cash < CrimeCashThreshold) return GangAction.Move;
        return tolerance < ChaosToleranceThreshold
            ? GangAction.Chaos
            : GangAction.Snitch;
    }

    public static GangAction SelectNoActionOrChaosContinuation(
        int force,
        int effectiveHeal,
        bool crackdownActive,
        GangAction olderAction)
    {
        if (CanHeal(force, effectiveHeal, StrictHealForceLimit))
            return crackdownActive ? GangAction.Move : GangAction.Heal;
        return olderAction == GangAction.Snitch
            ? GangAction.Chaos
            : GangAction.Move;
    }

    public static GangAction SelectHealContinuation(
        int force,
        int effectiveHeal,
        bool canSoloControl)
    {
        if (CanHeal(force, effectiveHeal, CommonHealForceLimit))
            return GangAction.Heal;
        return canSoloControl ? GangAction.Control : GangAction.Move;
    }

    public static GangAction SelectPostEquipmentContinuation(
        PlayerId actingPlayer,
        int sectorOwner,
        bool sectorOwnerIsHuman,
        int cash,
        AiDifficulty mentality,
        int tolerance)
    {
        if (sectorOwner is < MinimumRawSectorOwner or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(sectorOwner));
        if (!Enum.IsDefined(mentality))
            throw new ArgumentOutOfRangeException(nameof(mentality));

        var choosesCrime = sectorOwnerIsHuman
            ? cash >= CrimeCashThreshold && mentality >= AiDifficulty.Criminal
            : sectorOwner != actingPlayer.Value
                && sectorOwner > 0
                && cash >= CrimeCashThreshold
                && mentality == AiDifficulty.Goon;
        if (!choosesCrime) return GangAction.Move;
        return tolerance < ChaosToleranceThreshold
            ? GangAction.Chaos
            : GangAction.Snitch;
    }
}
