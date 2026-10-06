namespace Rechaos.Core.GameModel;

public sealed record MatchPlayerSetup(
    PlayerId Id,
    string Name,
    PlayerController Controller,
    short PortraitId = 0);

/// <summary>Chooses the computer command policy without changing AI resolution odds.</summary>
public enum AiPolicyMode : byte
{
    Original,
    Advanced
}

/// <summary>
/// The value of every deviation setting a match carries. Every <see cref="MatchSetup"/> states
/// them: a match started by the game takes them from <see cref="Defaults"/> and the command line,
/// and a test compared with the original takes <see cref="Original"/>.
/// </summary>
/// <param name="ComputerMovesToNeighboursOnly">
/// DEV-AI-007: whether a Move the computer planner plans must go to a neighbouring sector, as a
/// human's does. Off, the planner's Move goes to any sector, as in the original (RULE-MOVE-001).
/// </param>
/// <param name="ComputerHiresWhereHumansCan">
/// DEV-AI-008: whether a computer player's hire must go to a sector it controls or holds a gang
/// in, as a human's does. Off, a computer seat's hire goes to the sector the planner chose, as in
/// the original (RULE-AI-012, RULE-HIRE-001).
/// </param>
public readonly record struct MatchDeviations(
    bool ComputerMovesToNeighboursOnly,
    bool ComputerHiresWhereHumansCan)
{
    /// <summary>Every setting switched off: the original's behaviour, which the validation suite runs with.</summary>
    public static MatchDeviations Original { get; } = new(
        ComputerMovesToNeighboursOnly: false, ComputerHiresWhereHumansCan: false);

    /// <summary>Every setting at the Default that DEVIATIONS.md gives it (DEV-AI-007 on, DEV-AI-008 on).</summary>
    public static MatchDeviations Defaults { get; } = new(
        ComputerMovesToNeighboursOnly: true, ComputerHiresWhereHumansCan: true);
}

public sealed class MatchSetup
{
    public MatchSetup(
        ScenarioId scenario,
        GameDuration duration,
        int initialSeed,
        IReadOnlyList<MatchPlayerSetup> players,
        MatchDeviations deviations,
        AiDifficulty aiMentality = AiDifficulty.Criminal,
        bool allowSparsePlayerIds = false,
        AiPolicyMode aiPolicy = AiPolicyMode.Original)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Count is < 1 or > MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(players));
        var playerIds = players.Select(player => player.Id.Value).ToArray();
        if (playerIds.Any(id => id is < 0 or >= MatchLimits.PlayerCount)
            || playerIds.Distinct().Count() != playerIds.Length
            || !playerIds.SequenceEqual(playerIds.Order()))
            throw new ArgumentException(
                "Player identifiers must be unique, in range, and ordered.", nameof(players));
        if (!allowSparsePlayerIds
            && !playerIds.SequenceEqual(Enumerable.Range(0, players.Count)))
            throw new ArgumentException(
                "Player identifiers must be contiguous from zero.", nameof(players));
        if (players.Any(player => string.IsNullOrWhiteSpace(player.Name)))
            throw new ArgumentException("Player names cannot be blank.", nameof(players));
        if (players.Any(player => !Enum.IsDefined(player.Controller)))
            throw new ArgumentException("Player controller is invalid.", nameof(players));
        if (players.Any(player => player.PortraitId is < 0 or >= 16))
            throw new ArgumentException(
                "Player portrait is outside the original 16-entry atlas.", nameof(players));
        if (!Enum.IsDefined(aiMentality)) throw new ArgumentOutOfRangeException(nameof(aiMentality));
        if (!Enum.IsDefined(aiPolicy)) throw new ArgumentOutOfRangeException(nameof(aiPolicy));

        Scenario = scenario;
        Duration = duration;
        InitialSeed = initialSeed;
        Players = players.ToArray();
        AiMentality = aiMentality;
        AiPolicy = aiPolicy;
        Deviations = deviations;
        AllowsSparsePlayerIds = allowSparsePlayerIds;
    }

    public ScenarioId Scenario { get; }
    public GameDuration Duration { get; }
    public int InitialSeed { get; }
    public IReadOnlyList<MatchPlayerSetup> Players { get; }
    public AiDifficulty AiMentality { get; }
    public AiPolicyMode AiPolicy { get; }

    /// <summary>The deviation settings this match was started with.</summary>
    public MatchDeviations Deviations { get; }

    /// <summary>DEV-AI-007, from <see cref="Deviations"/>. No screen offers it.</summary>
    public bool ComputerMovesToNeighboursOnly => Deviations.ComputerMovesToNeighboursOnly;

    /// <summary>
    /// DEV-AI-008, from <see cref="Deviations"/>. A human seat the planner plays for a simulation
    /// keeps the human test either way.
    /// </summary>
    public bool ComputerHiresWhereHumansCan => Deviations.ComputerHiresWhereHumansCan;
    public bool AllowsSparsePlayerIds { get; }

    internal MatchSetup WithController(PlayerId playerId, PlayerController controller)
    {
        if (!Enum.IsDefined(controller)) throw new ArgumentOutOfRangeException(nameof(controller));
        if (!Players.Any(player => player.Id == playerId))
            throw new ArgumentOutOfRangeException(nameof(playerId));
        return new MatchSetup(
            Scenario,
            Duration,
            InitialSeed,
            Players.Select(player => player.Id == playerId
                ? player with { Controller = controller }
                : player).ToArray(),
            Deviations,
            AiMentality,
            AllowsSparsePlayerIds,
            AiPolicy);
    }
}
