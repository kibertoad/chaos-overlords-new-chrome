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

    /// <summary>
    /// FND-AI-057: the crime gate makes two tests in sequence. An owner that reads as human
    /// (selector 0x35) with cash of at least 50 at Criminal or above passes the first; anything
    /// else, a human owner included, gets the second, which needs an owner query other than the
    /// player and above 0, cash above 49 and Mentality Goon.
    /// </summary>
    public static GangAction SelectPostEquipmentContinuation(
        PlayerId actingPlayer,
        int ownerQuery,
        bool ownerIsHuman,
        int cash,
        AiDifficulty mentality,
        int tolerance)
    {
        if (ownerQuery is < MinimumRawSectorOwner or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(ownerQuery));
        if (!Enum.IsDefined(mentality))
            throw new ArgumentOutOfRangeException(nameof(mentality));

        var choosesCrime =
            (ownerIsHuman && cash >= CrimeCashThreshold && mentality >= AiDifficulty.Criminal)
            || (ownerQuery != actingPlayer.Value
                && ownerQuery > 0
                && cash >= CrimeCashThreshold
                && mentality == AiDifficulty.Goon);
        if (!choosesCrime) return GangAction.Move;
        return tolerance < ChaosToleranceThreshold
            ? GangAction.Chaos
            : GangAction.Snitch;
    }

    /// <summary>
    /// FND-AI-057: after previous Attack, Hide or Move, a gang that neither attacks nor moves off
    /// its own sector heals, takes the sector or passes the Snitch gate: an owner that reads as
    /// human, and either a hostile owner at Criminal or above or Mentality Crime Lord. A passing
    /// gate with cash above 50 snitches; everything else moves.
    /// </summary>
    public static GangAction SelectAfterAttackHideOrMove(
        int force,
        int effectiveHeal,
        bool canSoloControl,
        bool ownerIsHuman,
        bool ownerIsHostile,
        AiDifficulty mentality,
        int cash)
    {
        if (CanHeal(force, effectiveHeal, CommonHealForceLimit)) return GangAction.Heal;
        if (canSoloControl) return GangAction.Control;
        var snitchGate = ownerIsHuman
            && ((ownerIsHostile && mentality >= AiDifficulty.Criminal)
                || mentality == AiDifficulty.CrimeLord);
        return snitchGate ? SelectStrictCashContinuation(cash) : GangAction.Move;
    }
}
