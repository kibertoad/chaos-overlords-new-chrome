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
        AllowsSparsePlayerIds = allowSparsePlayerIds;
    }

    public ScenarioId Scenario { get; }
    public GameDuration Duration { get; }
    public int InitialSeed { get; }
    public IReadOnlyList<MatchPlayerSetup> Players { get; }
    public AiDifficulty AiMentality { get; }
    public AiPolicyMode AiPolicy { get; }
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
            AiPolicy);
    }
}
