namespace Rechaos.Core.GameModel;

public enum ScenarioId
{
    Greed,
    Power,
    Acceptance,
    Dominance,
    KillEmAll,
    Big40,
    Eliminate,
    Siege,
    BigMan,
    Armageddon
}

public enum GameDuration
{
    SixMonths,
    OneYear,
    TwoYears,
    FourYears
}

public readonly record struct DominanceWeights(int Cash, int Support, int ControlledSector);

public sealed record ScenarioDefinition(
    ScenarioId Id,
    string Name,
    bool IsTimed,
    string Objective);

/// <summary>
/// Scenario rules transcribed from the original manual. Values are exact to the
/// manual but remain subject to behavioral verification against the original binary.
/// </summary>
public static class ScenarioCatalog
{
    public static readonly IReadOnlyList<ScenarioDefinition> All =
    [
        new(ScenarioId.Greed, "GREED", true, "Have the most cash when time expires."),
        new(ScenarioId.Power, "POWER", true, "Control the most sectors when time expires."),
        new(ScenarioId.Acceptance, "ACCEPTANCE", true, "Have the most support when time expires."),
        new(ScenarioId.Dominance, "DOMINANCE", true, "Lead the weighted cash, support, and sector score."),
        new(ScenarioId.KillEmAll, "KILL 'EM ALL", false, "Be the sole surviving Overlord."),
        new(ScenarioId.Big40, "BIG 40", false, "Be first to control 40 sectors."),
        new(ScenarioId.Eliminate, "ELIMINATE", false, "Eliminate every opposing Right Hands gang."),
        new(ScenarioId.Siege, "SIEGE", false, "Control all six important starting sectors simultaneously."),
        new(ScenarioId.BigMan, "BIG MAN", false, "Accumulate 40 points from the four central sectors."),
        new(ScenarioId.Armageddon, "ARMAGEDDON", false, "Control all 64 sectors.")
    ];

    public static ScenarioDefinition Get(ScenarioId id) => All.Single(scenario => scenario.Id == id);

    public static int Turns(GameDuration duration) => duration switch
    {
        GameDuration.SixMonths => 26,
        GameDuration.OneYear => 52,
        GameDuration.TwoYears => 104,
        GameDuration.FourYears => 208,
        _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, null)
    };

    public static DominanceWeights Weights(GameDuration duration) => duration switch
    {
        GameDuration.SixMonths => new(1, 10, 30),
        GameDuration.OneYear => new(1, 30, 100),
        GameDuration.TwoYears => new(1, 75, 250),
        GameDuration.FourYears => new(1, 300, 1_000),
        _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, null)
    };

    public static long TimedScore(
        ScenarioId scenario,
        GameDuration duration,
        PlayerScoreState state) => scenario switch
    {
        ScenarioId.Greed => state.Cash,
        ScenarioId.Power => state.ControlledSectors,
        ScenarioId.Acceptance => state.Support,
        ScenarioId.Dominance => DominanceScore(duration, state),
        _ => throw new ArgumentException($"{scenario} is not a timed scoring scenario.", nameof(scenario))
    };

    public static bool HasObjectiveVictory(ScenarioId scenario, PlayerScoreState state) => scenario switch
    {
        ScenarioId.KillEmAll => state.IsAlive && state.OpponentsAlive == 0,
        ScenarioId.Big40 => state.ControlledSectors >= 40,
        ScenarioId.Eliminate => state.OpposingRightHandsAlive == 0,
        ScenarioId.Siege => state.ImportantSectorsControlled >= 6,
        ScenarioId.BigMan => state.BigManPoints >= 40,
        ScenarioId.Armageddon => state.ControlledSectors >= MatchLimits.SectorCount,
        _ => false
    };

    private static long DominanceScore(GameDuration duration, PlayerScoreState state)
    {
        var weights = Weights(duration);
        return (long)state.Cash * weights.Cash
            + (long)state.Support * weights.Support
            + (long)state.ControlledSectors * weights.ControlledSector;
    }
}

public readonly record struct PlayerScoreState(
    int Cash,
    int Support,
    int ControlledSectors,
    bool IsAlive = true,
    int OpponentsAlive = 0,
    int OpposingRightHandsAlive = 0,
    int ImportantSectorsControlled = 0,
    int BigManPoints = 0);
