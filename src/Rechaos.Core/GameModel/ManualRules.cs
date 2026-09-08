namespace Rechaos.Core.GameModel;

/// <summary>
/// Small, unambiguous rules stated by the original manual. These are separated
/// from binary-verified rules so intended behavior and compatibility evidence
/// are not conflated.
/// </summary>
public static class ManualRules
{
    public const int MaximumForce = 10;
    public const int BribeCost = 3;
    public const int BribeToleranceIncrease = 5;
    public const int SnitchToleranceDecrease = 3;
    public const int MinimumTolerance = 0;
    public const int MaximumTolerance = 40;
    public const int PoliceCombat = 20;
    public const int PoliceDetect = 12;
    public const int HealBaseDice = 4;
    public const int ControlledSectorTax = 1;

    public static int ApplyBribe(int tolerance)
    {
        ValidateTolerance(tolerance);
        return Math.Min(MaximumTolerance, tolerance + BribeToleranceIncrease);
    }

    public static int ApplySnitch(int tolerance)
    {
        ValidateTolerance(tolerance);
        return Math.Max(MinimumTolerance, tolerance - SnitchToleranceDecrease);
    }

    public static bool IsDieSuccess(int value)
    {
        if (value is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(value), value, "A die must be 1-6.");
        return value >= 4;
    }

    public static int CountSuccesses(IEnumerable<int> rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        return rolls.Count(IsDieSuccess);
    }

    /// <summary>Manual-stated chance that police detect a gang during crackdown.</summary>
    public static int PoliceDetectionPercent(int stealth)
    {
        if (stealth < 0) throw new ArgumentOutOfRangeException(nameof(stealth));
        return Math.Clamp(100 - Math.Max(0, stealth - 5) * 5, 0, 100);
    }

    public static int RestoreForce(int currentForce, int successes)
    {
        if (currentForce is < 0 or > MaximumForce) throw new ArgumentOutOfRangeException(nameof(currentForce));
        if (successes < 0) throw new ArgumentOutOfRangeException(nameof(successes));
        return Math.Min(MaximumForce, currentForce + successes);
    }

    public static int HealDiceCount(int healSkill) => Math.Max(0, HealBaseDice + healSkill);

    public static int ResearchDiceCount(int force, int researchSkill)
    {
        if (force is < 0 or > MaximumForce) throw new ArgumentOutOfRangeException(nameof(force));
        return Math.Max(0, force + researchSkill);
    }

    public static int ApplyResearchProgress(int remaining, int successes)
    {
        if (remaining < 0) throw new ArgumentOutOfRangeException(nameof(remaining));
        if (successes < 0) throw new ArgumentOutOfRangeException(nameof(successes));
        return Math.Max(0, remaining - successes);
    }

    public static int InfluenceDiceCount(IEnumerable<(int Force, int Influence)> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var total = 0;
        foreach (var participant in participants)
        {
            if (participant.Force is < 0 or > MaximumForce)
                throw new ArgumentOutOfRangeException(nameof(participants));
            total = checked(total + participant.Force + participant.Influence);
        }
        return Math.Max(0, total);
    }

    public static int ApplyInfluenceProgress(int resistance, int successes)
    {
        if (resistance < 0) throw new ArgumentOutOfRangeException(nameof(resistance));
        if (successes < 0) throw new ArgumentOutOfRangeException(nameof(successes));
        return Math.Max(0, resistance - successes);
    }

    public static int ChaosDiceCount(IEnumerable<(int Force, int Chaos)> gangs, int sectorIncome)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        var total = sectorIncome;
        foreach (var gang in gangs)
        {
            if (gang.Force is < 0 or > MaximumForce)
                throw new ArgumentOutOfRangeException(nameof(gangs));
            total = checked(total + gang.Force + gang.Chaos);
        }
        return Math.Max(0, total);
    }

    public static int ChaosIncome(int successes, bool controlsSector)
    {
        if (successes < 0) throw new ArgumentOutOfRangeException(nameof(successes));
        return controlsSector ? successes : successes / 2;
    }

    public static bool TriggersCrackdown(int chaos, int tolerance)
    {
        if (chaos < 0) throw new ArgumentOutOfRangeException(nameof(chaos));
        ValidateTolerance(tolerance);
        return chaos > tolerance;
    }

    public static int CombatRating(EffectiveStatistics statistics, short? weaponType)
    {
        var skill = weaponType switch
        {
            null => checked(statistics.Strength + statistics.Fighting + statistics.MartialArts),
            0 => statistics.Strength,
            1 => checked(statistics.Strength + statistics.Blade),
            2 => statistics.Range,
            _ => throw new ArgumentOutOfRangeException(nameof(weaponType), weaponType, "A weapon type must be melee, blade, or ranged.")
        };
        return checked(statistics.Combat + skill);
    }

    public static int AttackDiceCount(int force, int combatRating, int defense)
    {
        if (force is < 0 or > MaximumForce) throw new ArgumentOutOfRangeException(nameof(force));
        return Math.Max(0, checked(force + combatRating - defense));
    }

    public static int RetaliationDamage(int successes)
    {
        if (successes < 0) throw new ArgumentOutOfRangeException(nameof(successes));
        return successes / 2;
    }

    public static bool SuppressesRetaliation(EffectiveStatistics attacker, short? attackerWeaponType) =>
        attackerWeaponType is null && attacker.MartialArts > 0;

    public static int HiddenAttackHitPercent(int detect, int stealth)
    {
        var chance = 50L + ((long)detect - stealth) * 5;
        return (int)Math.Clamp(chance, 0, 100);
    }

    public static int SectorDetectionStrength(IEnumerable<int> gangDetect)
    {
        ArgumentNullException.ThrowIfNull(gangDetect);
        var values = gangDetect.OrderDescending().ToArray();
        if (values.Length == 0) return 0;
        var total = values[0];
        foreach (var detect in values.Skip(1)) total = checked(total + DetectionAssist(detect));
        return total;
    }

    private static int DetectionAssist(int detect) => detect switch
    {
        < 0 => 0,
        <= 10 => 1,
        <= 12 => 2,
        <= 14 => 3,
        <= 16 => 4,
        <= 18 => 5,
        _ => 6
    };

    public static int ControlStrength(IEnumerable<(int Force, int Control)> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        var total = 0;
        foreach (var gang in gangs)
        {
            if (gang.Force is < 0 or > MaximumForce)
                throw new ArgumentOutOfRangeException(nameof(gangs));
            total = checked(total + gang.Force + gang.Control);
        }
        return total;
    }

    public static int ControlMargin(int attack, int sectorIncome, int defense = 0, int support = 0) =>
        checked(attack - sectorIncome - defense - support);

    private static void ValidateTolerance(int tolerance)
    {
        if (tolerance is < MinimumTolerance or > MaximumTolerance)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be 0-40.");
    }
}
