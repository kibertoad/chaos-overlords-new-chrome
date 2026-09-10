namespace Rechaos.Core.GameModel;

internal enum OriginalResolutionBand
{
    Goon = 0,
    Standard = 1,
    Expert = 2
}

/// <summary>Difficulty calibration recovered from the original whole-turn resolver.</summary>
internal static class OriginalResolutionRules
{
    public static OriginalResolutionBand Band(MatchState state, PlayerId player)
    {
        var setup = state.FindPlayer(player)?.Setup
            ?? throw new ArgumentOutOfRangeException(nameof(player));
        return Band(setup.Controller, state.Setup.AiMentality);
    }

    internal static OriginalResolutionBand Band(
        PlayerController controller,
        AiDifficulty mentality)
    {
        if (controller == PlayerController.Human) return OriginalResolutionBand.Standard;
        return mentality switch
        {
            AiDifficulty.Goon => OriginalResolutionBand.Goon,
            AiDifficulty.Criminal => OriginalResolutionBand.Standard,
            AiDifficulty.CrimeLord or AiDifficulty.HomicidalManiac => OriginalResolutionBand.Expert,
            _ => throw new ArgumentOutOfRangeException(nameof(mentality))
        };
    }

    public static int ActionPool(OriginalResolutionBand band, GangAction action, int pool)
    {
        if (pool <= 0) return 0;
        return band == OriginalResolutionBand.Goon
            && action is GangAction.Chaos or GangAction.Influence or GangAction.Research
                ? pool - pool / 5
                : pool;
    }

    public static int SuccessThreshold(OriginalResolutionBand band, GangAction action) => action switch
    {
        GangAction.Heal or GangAction.Influence or GangAction.Chaos =>
            band == OriginalResolutionBand.Expert ? 4 : 5,
        GangAction.Research => band == OriginalResolutionBand.Expert ? 5 : 6,
        GangAction.Attack => band switch
        {
            OriginalResolutionBand.Goon => 6,
            OriginalResolutionBand.Standard => 5,
            OriginalResolutionBand.Expert => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(band))
        },
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Action has no calibrated dice threshold.")
    };

    public static int CountSuccesses(IEnumerable<int> rolls, int threshold)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        if (threshold is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(threshold));
        return rolls.Count(roll => roll >= threshold);
    }

    public static int AdjustDefense(OriginalResolutionBand defenderBand, int defense) =>
        defenderBand == OriginalResolutionBand.Goon ? defense - defense / 4 : defense;

    public static int MainAttackDamage(int pool, int successes) =>
        pool > 0 ? Math.Max(successes, pool / 4) : 0;

    public static int RetaliationThreshold(OriginalResolutionBand defenderBand) =>
        defenderBand == OriginalResolutionBand.Expert ? 4 : 5;

    public static int HiddenEvasionThreshold(
        OriginalResolutionBand attackerBand,
        int attackerDetect,
        int targetStealth) =>
        checked(targetStealth
            + (attackerBand == OriginalResolutionBand.Expert ? 10 : 14)
            - attackerDetect);

    public static int HiddenHitPercent(int evasionThreshold) =>
        Math.Clamp(21 - evasionThreshold, 0, 20) * 5;

    public static int CrackdownContribution(
        OriginalResolutionBand band,
        bool controlsSector,
        int successes)
    {
        if (successes < 0) throw new ArgumentOutOfRangeException(nameof(successes));
        return band == OriginalResolutionBand.Expert && controlsSector
            ? successes - successes / 4
            : successes;
    }
}
