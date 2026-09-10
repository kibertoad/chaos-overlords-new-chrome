namespace Rechaos.Core.GameModel;

/// <summary>
/// Unadjusted turn schedule from the original outer AI planner at 0x00458fa0.
/// The planner mutates the scheduled slot using gang-family quotas before applying
/// this mapping; those adjustments remain separate until their x87 comparisons
/// have been recovered at instruction level.
/// </summary>
internal static class OriginalAiHireRoleRules
{
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
