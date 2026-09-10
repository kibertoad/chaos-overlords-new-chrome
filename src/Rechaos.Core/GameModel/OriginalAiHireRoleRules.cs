namespace Rechaos.Core.GameModel;

/// <summary>
/// Unadjusted turn schedule from the original outer AI planner at 0x00458fa0.
/// The planner mutates the scheduled slot using gang-family quotas before applying
/// this mapping; those adjustments remain separate until their x87 comparisons
/// have been recovered at instruction level.
/// </summary>
internal static class OriginalAiHireRoleRules
{
    public static int CalculateHireGangLimit(
        ScenarioId scenario,
        int activeGangCount,
        int ownedSectorCount,
        int cash,
        bool hasNeutralSector)
    {
        if (!Enum.IsDefined(scenario))
            throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        ArgumentOutOfRangeException.ThrowIfNegative(activeGangCount);
        if (ownedSectorCount is < 0 or > MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(ownedSectorCount));

        int limit;
        if (!hasNeutralSector)
            limit = cash > 300 ? MatchLimits.GangsPerPlayer : checked(activeGangCount + ownedSectorCount);
        else if (scenario == ScenarioId.Greed)
            limit = checked(ownedSectorCount * 3 / 2);
        else if (scenario is ScenarioId.Power or ScenarioId.Acceptance or ScenarioId.Dominance)
            limit = checked(ownedSectorCount * 2);
        else
            limit = checked(ownedSectorCount * 4);

        return Math.Min(MatchLimits.GangsPerPlayer, limit);
    }

    public static bool ShouldAttemptHire(
        ScenarioId scenario,
        int activeGangCount,
        int hireGangLimit,
        int turnsRemaining,
        GameDuration duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(activeGangCount);
        ArgumentOutOfRangeException.ThrowIfNegative(hireGangLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(turnsRemaining);

        return scenario switch
        {
            ScenarioId.Greed => activeGangCount <= hireGangLimit
                && ScenarioCatalog.Turns(duration) / 8 < turnsRemaining,
            ScenarioId.Power or ScenarioId.Acceptance or ScenarioId.Dominance =>
                activeGangCount <= hireGangLimit && turnsRemaining > 2,
            ScenarioId.KillEmAll or ScenarioId.Big40 or ScenarioId.Eliminate
                or ScenarioId.Siege or ScenarioId.Armageddon =>
                activeGangCount <= hireGangLimit,
            // The original Big Man case enters its turn schedule directly.
            ScenarioId.BigMan => true,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };
    }

    public static OriginalAiHireRoleSelection SelectAdjusted(
        ScenarioId scenario,
        int turn,
        OriginalAiHireAdjustmentInputs inputs) => scenario switch
        {
            ScenarioId.Greed => SelectGreedAdjusted(turn, inputs),
            ScenarioId.Power or ScenarioId.KillEmAll or ScenarioId.Big40 =>
                SelectPowerAdjusted(scenario, turn, inputs),
            ScenarioId.Acceptance => SelectAcceptanceAdjusted(turn, inputs),
            ScenarioId.Dominance => SelectDominanceAdjusted(turn, inputs),
            ScenarioId.Eliminate => SelectEliminateAdjusted(turn, inputs),
            ScenarioId.Siege => SelectSiegeAdjusted(turn, inputs),
            ScenarioId.BigMan => SelectBigManAdjusted(turn, inputs),
            ScenarioId.Armageddon => SelectArmageddonAdjusted(turn, inputs),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

    public static OriginalAiHireRoleSelection SelectScheduled(ScenarioId scenario, int turn)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));

        var schedule = scenario switch
        {
            ScenarioId.Greed => Greed,
            ScenarioId.Power or ScenarioId.KillEmAll or ScenarioId.Big40 => Power,
            ScenarioId.Acceptance => Acceptance,
            ScenarioId.Dominance => Dominance,
            ScenarioId.Eliminate => Eliminate,
            ScenarioId.Siege => Siege,
            ScenarioId.BigMan => BigMan,
            ScenarioId.Armageddon => Armageddon,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        return schedule[turn % schedule.Length];
    }

    /// <summary>
    /// Applies the instruction-verified adjustment block shared verbatim by
    /// Power, Kill 'Em All, and Big 40 before their final role switch.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectPowerAdjusted(
        ScenarioId scenario,
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (scenario is not (ScenarioId.Power or ScenarioId.KillEmAll or ScenarioId.Big40))
            throw new ArgumentException("Only scenarios sharing the original Power branch are valid.", nameof(scenario));
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.TurnsRemaining);

        var slot = turn % 10;
        if (inputs.TurnsRemaining < 10 && slot == 4) slot = 1;
        if (slot == 8 && (inputs.TurnsRemaining < 10 || inputs.Cash < 100)) slot = 1;

        var changesSix = !inputs.HasVisibleHostileSector
            || inputs.HasFamily6CoveringFirstHostileSector
            || inputs.Family5Count < 1
            || inputs.Family7Count < 1
            || inputs.PreviousRole == 6;
        if (!changesSix)
        {
            slot = 6;
        }
        else if (slot == 6)
        {
            slot = inputs.Family7Count == 0
                ? 4
                : inputs.Family5Count == 0 ? 9 : 8;
        }

        var durationFactor = ScenarioCatalog.Turns(inputs.Duration) / 52f;
        if (slot == 8 && inputs.Family2Count >= durationFactor * 4f) slot = 0;
        if (slot == 6 && inputs.Family6Or12Count >= durationFactor * 4f) slot = 0;
        if (slot == 9 && inputs.Family5Count >= durationFactor * 3f) slot = 0;
        if (slot == 4 && inputs.Family7Count >= durationFactor) slot = 0;
        if (inputs.Family0Or4Count < 4) slot = 0;

        return Power[slot];
    }

    /// <summary>
    /// Applies the instruction-verified Eliminate adjustment block.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectEliminateAdjusted(
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));

        var slot = turn % 10;
        var durationFactor = ScenarioCatalog.Turns(inputs.Duration) / 52f;
        if (slot == 9 && inputs.Family5Count >= durationFactor * 3f) slot = 0;
        if (slot == 4 && inputs.Family7Count >= durationFactor) slot = 0;
        if (inputs.Family0Or4Count < 4) slot = 0;

        return Eliminate[slot];
    }

    /// <summary>
    /// Applies the instruction-verified Siege adjustment block.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectSiegeAdjusted(
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));

        var slot = turn % 10;
        var durationFactor = ScenarioCatalog.Turns(inputs.Duration) / 52f;
        if (slot is 3 or 6 or 8 && inputs.Family3Count >= durationFactor * 4f) slot = 0;
        if (slot is 5 or 7 && inputs.Family6Or12Count >= durationFactor * 6f) slot = 0;
        if (slot == 9 && (inputs.Family2Count >= durationFactor * 2f
            || inputs.Cash < durationFactor * 100f)) slot = 0;
        if (slot == 1 && inputs.Family7Count >= durationFactor) slot = 0;
        if (inputs.Family0Or4Count < 5) slot = 0;
        if (inputs.Family6Or12Count < 1) slot = 5;

        return Siege[slot];
    }

    /// <summary>
    /// Applies the instruction-verified Big Man adjustment block.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectBigManAdjusted(
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));

        var slot = turn % 10;
        if (slot is 0 or 2 or 5 or 7 && inputs.Family0Or4Count > 5) slot = 4;
        return BigMan[slot];
    }

    /// <summary>
    /// Applies the instruction-verified Armageddon adjustment block.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectArmageddonAdjusted(
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));

        var slot = turn % 10;
        var changesFive = !inputs.HasVisibleHostileSector
            || inputs.HasFamily6CoveringFirstHostileSector
            || inputs.Family3Count < 1
            || inputs.PreviousRole == 5;
        if (!changesFive)
        {
            slot = 5;
        }
        else if (slot == 5)
        {
            slot = inputs.Family3Count == 0 ? 9 : 3;
        }

        var durationFactor = ScenarioCatalog.Turns(inputs.Duration) / 52f;
        if (slot == 3 && inputs.Family2Count >= durationFactor * 4f) slot = 0;
        if (slot == 5 && inputs.Family6Or12Count >= durationFactor * 4f) slot = 0;
        if (slot == 9 && inputs.Family3Count >= durationFactor * 3f) slot = 0;
        if (inputs.Family0Or4Count < 4) slot = 0;
        if (inputs.Family2Count < 1) slot = 3;

        return Armageddon[slot];
    }

    /// <summary>
    /// Applies the instruction-verified Greed adjustment block after its
    /// separate duration/remaining-turn hire-attempt gate has passed.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectGreedAdjusted(
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.TurnsRemaining);

        var slot = turn % 10;
        if (inputs.TurnsRemaining < 10 && slot is 1 or 3 or 6 or 8) slot = 0;
        if (slot == 9 && inputs.HasHigherScoringPlayer
            && (inputs.TurnsRemaining < 10 || inputs.Cash < 100)) slot = 0;

        var changesFive = !inputs.HasVisibleHostileSector
            || inputs.HasFamily6CoveringFirstHostileSector
            || inputs.Family3Count < 1
            || inputs.Family7Count < 1
            || inputs.PreviousRole == 5;
        if (!changesFive)
        {
            slot = 5;
        }
        else if (slot == 5)
        {
            slot = inputs.Family7Count == 0
                ? 1
                : inputs.Family3Count == 0 ? 3 : 9;
        }

        var durationFactor = ScenarioCatalog.Turns(inputs.Duration) / 52f;
        if (slot is 3 or 6 or 8 && inputs.Family3Count >= durationFactor * 4f) slot = 0;
        if (slot == 5 && inputs.Family6Or12Count >= durationFactor * 2f) slot = 0;
        if (slot == 9 && (inputs.Family2Count >= durationFactor * 2f
            || inputs.Cash < durationFactor * 100f)) slot = 0;
        if (slot == 1 && inputs.Family7Count >= durationFactor) slot = 0;
        if (inputs.Family0Or4Count < 5) slot = 0;

        return Greed[slot];
    }

    /// <summary>
    /// Applies the instruction-verified Acceptance adjustment block.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectAcceptanceAdjusted(
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.TurnsRemaining);

        var slot = turn % 10;
        if (inputs.TurnsRemaining < 5 && slot is 1 or 4 or 6 or 8) slot = 0;
        if (slot == 5 && (inputs.TurnsRemaining < 10 || inputs.Cash < 100)) slot = 0;

        var changesTwo = !inputs.HasVisibleHostileSector
            || inputs.HasFamily6CoveringFirstHostileSector
            || inputs.Family5Count < 1
            || inputs.Family7Count < 1
            || inputs.PreviousRole == 2;
        if (!changesTwo)
        {
            slot = 2;
        }
        else if (slot == 2)
        {
            slot = inputs.Family7Count == 0
                ? 1
                : inputs.Family5Count == 0 ? 4 : 5;
        }

        var durationFactor = ScenarioCatalog.Turns(inputs.Duration) / 52f;
        if (slot is 4 or 6 or 8 && inputs.Family5Count >= durationFactor * 6f) slot = 0;
        if (slot == 5 && inputs.Family2Count >= durationFactor * 2f) slot = 0;
        if (slot == 2 && inputs.Family6Or12Count >= durationFactor * 3f) slot = 0;
        if (slot == 1 && inputs.Family7Count >= durationFactor) slot = 0;
        if (inputs.Family0Or4Count < 4) slot = 0;

        return Acceptance[slot];
    }

    /// <summary>
    /// Applies the instruction-verified Dominance adjustment block.
    /// </summary>
    public static OriginalAiHireRoleSelection SelectDominanceAdjusted(
        int turn,
        OriginalAiHireAdjustmentInputs inputs)
    {
        if (turn < 0) throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.TurnsRemaining);

        var slot = turn % 11;
        if (inputs.TurnsRemaining < 8 && slot is 2 or 3 or 4 or 6 or 8) slot = 0;
        if (slot == 5 && (inputs.TurnsRemaining < 10 || inputs.Cash < 100)) slot = 0;

        var changesTen = !inputs.HasVisibleHostileSector
            || inputs.HasFamily6CoveringFirstHostileSector
            || inputs.Family3Count < 1
            || inputs.Family7Count < 1
            || inputs.PreviousRole == 10;
        if (!changesTen)
        {
            slot = 10;
        }
        else if (slot == 10)
        {
            slot = inputs.Family7Count == 0
                ? 4
                : inputs.Family3Count == 0
                    ? 2
                    : inputs.Family5Count == 0 ? 3 : 5;
        }

        var durationFactor = ScenarioCatalog.Turns(inputs.Duration) / 52f;
        if (slot is 3 or 6 && inputs.Family5Count >= durationFactor * 3f) slot = 0;
        if (slot is 2 or 8 && inputs.Family3Count >= durationFactor * 3f) slot = 0;
        if (slot == 5 && inputs.Family2Count >= durationFactor * 2f) slot = 0;
        if (slot == 10 && inputs.Family6Or12Count >= durationFactor * 3f) slot = 0;
        if (slot == 4 && inputs.Family7Count >= durationFactor) slot = 0;
        if (inputs.Family0Or4Count < 4) slot = 0;

        return Dominance[slot];
    }

    private static readonly OriginalAiHireRoleSelection[] Greed =
    [
        S(0, 1), S(4, 6), S(0, 1), S(2, 2), S(0, 1),
        S(3, 4), S(2, 2), S(0, 1), S(2, 2), S(3, 3)
    ];

    private static readonly OriginalAiHireRoleSelection[] Power =
    [
        S(0, 0), S(1, 1), S(0, 0), S(0, 0), S(4, 6),
        S(0, 0), S(3, 4), S(0, 0), S(3, 3), S(2, 5)
    ];

    private static readonly OriginalAiHireRoleSelection[] Acceptance =
    [
        S(0, 1), S(4, 6), S(3, 4), S(0, 1), S(2, 2),
        S(3, 3), S(2, 2), S(0, 1), S(2, 2), S(0, 1)
    ];

    // Dominance alone uses currentTurn % 11 in the original.
    private static readonly OriginalAiHireRoleSelection[] Dominance =
    [
        S(0, 1), S(0, 1), S(2, 5), S(2, 2), S(4, 6), S(3, 3),
        S(2, 2), S(0, 1), S(2, 5), S(0, 1), S(3, 4)
    ];

    private static readonly OriginalAiHireRoleSelection[] Eliminate =
    [
        S(0, 0), S(1, 2), S(0, 0), S(1, 2), S(4, 6),
        S(1, 1), S(1, 2), S(0, 0), S(1, 1), S(2, 5)
    ];

    private static readonly OriginalAiHireRoleSelection[] Siege =
    [
        S(0, 1), S(4, 6), S(0, 1), S(2, 2), S(0, 1),
        S(3, 4), S(2, 2), S(3, 4), S(2, 2), S(5, 3)
    ];

    private static readonly OriginalAiHireRoleSelection[] BigMan =
    [
        S(0, 0), S(1, 2), S(0, 0), S(1, 1), S(1, 1),
        S(2, 3), S(1, 2), S(0, 0), S(1, 1), S(1, 2)
    ];

    private static readonly OriginalAiHireRoleSelection[] Armageddon =
    [
        S(0, 0), S(1, 1), S(0, 0), S(3, 3), S(0, 0),
        S(3, 4), S(0, 0), S(3, 3), S(0, 0), S(2, 5)
    ];

    private static OriginalAiHireRoleSelection S(int rankingMode, int role) =>
        new(rankingMode, role);
}

internal readonly record struct OriginalAiHireRoleSelection(int RankingMode, int Role);

internal readonly record struct OriginalAiHireAdjustmentInputs(
    int TurnsRemaining,
    int Cash,
    bool HasHigherScoringPlayer,
    bool HasVisibleHostileSector,
    bool HasFamily6CoveringFirstHostileSector,
    int Family5Count,
    int Family7Count,
    int PreviousRole,
    int Family2Count,
    int Family3Count,
    int Family6Or12Count,
    int Family0Or4Count,
    GameDuration Duration);
