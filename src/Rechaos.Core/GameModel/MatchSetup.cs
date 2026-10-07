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

public sealed class MatchSetup
{
    public MatchSetup(
        ScenarioId scenario,
        GameDuration duration,
        int initialSeed,
        IReadOnlyList<MatchPlayerSetup> players,
        AiDifficulty aiMentality = AiDifficulty.Criminal,
        bool allowSparsePlayerIds = false,
        AiPolicyMode aiPolicy = AiPolicyMode.Original,
        bool computerMovesToNeighboursOnly = true,
        bool computerHiresWhereHumansCan = true)
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
        // A name of spaces is a name: the original's name editor keeps one (EXP-UI-053), and the
        // setup screen stores it (RULE-SETUP-009). Only a missing name is refused.
        if (players.Any(player => string.IsNullOrEmpty(player.Name)))
            throw new ArgumentException("Player names cannot be empty.", nameof(players));
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
        ComputerMovesToNeighboursOnly = computerMovesToNeighboursOnly;
        ComputerHiresWhereHumansCan = computerHiresWhereHumansCan;
        AllowsSparsePlayerIds = allowSparsePlayerIds;
    }

    public ScenarioId Scenario { get; }
    public GameDuration Duration { get; }
    public int InitialSeed { get; }
    public IReadOnlyList<MatchPlayerSetup> Players { get; }
    public AiDifficulty AiMentality { get; }
    public AiPolicyMode AiPolicy { get; }

    /// <summary>
    /// DEV-AI-007: whether a Move the computer planner plans must go to a neighbouring sector, as a
    /// human's does. Off, the planner's Move goes to any sector, as in the original (RULE-MOVE-001).
    /// No screen offers it; the game's command line and the tests switch it off.
    /// </summary>
    public bool ComputerMovesToNeighboursOnly { get; }

    /// <summary>
    /// DEV-AI-008: whether a computer player's hire must go to a sector it controls or holds a gang
    /// in, as a human's does. Off, a computer seat's hire goes to the sector the planner chose, as
    /// in the original (RULE-AI-012, RULE-HIRE-001); a human seat the planner plays for a
    /// simulation keeps the human test either way.
    /// </summary>
    public bool ComputerHiresWhereHumansCan { get; }
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
            AiMentality,
            AllowsSparsePlayerIds,
            AiPolicy,
            ComputerMovesToNeighboursOnly,
            ComputerHiresWhereHumansCan);
    }
}
