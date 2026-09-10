namespace Rechaos.Core.GameModel;

/// <summary>
/// Original-compatible per-player reaction values and directional attitudes.
/// The fixed six-player storage mirrors the original executable, including
/// inactive player slots.
/// </summary>
public sealed class AiStrategicState
{
    public const int MinimumAttitude = -10;
    public const int MaximumAttitude = 10;
    private readonly int[] _reactions;
    private readonly int[] _attitudes;

    private AiStrategicState(IReadOnlyList<int> reactions, IReadOnlyList<int> attitudes)
    {
        ArgumentNullException.ThrowIfNull(reactions);
        ArgumentNullException.ThrowIfNull(attitudes);
        if (reactions.Count != MatchLimits.PlayerCount)
            throw new ArgumentException("AI reactions must contain all six original player slots.", nameof(reactions));
        if (attitudes.Count != MatchLimits.PlayerCount * MatchLimits.PlayerCount)
            throw new ArgumentException("AI attitudes must contain the original six-by-six matrix.", nameof(attitudes));
        if (!reactions.All(value => value == 0)
            && reactions.Any(value => value is < 3 or > 6))
            throw new ArgumentOutOfRangeException(nameof(reactions));
        if (attitudes.Any(value => value is < MinimumAttitude or > MaximumAttitude))
            throw new ArgumentOutOfRangeException(nameof(attitudes));
        _reactions = reactions.ToArray();
        _attitudes = attitudes.ToArray();
    }

    public int Reaction(PlayerId player) => _reactions[PlayerIndex(player)];

    public int Attitude(PlayerId observer, PlayerId other) =>
        _attitudes[MatrixIndex(observer, other)];

    public bool IsHostile(PlayerId observer, PlayerId other) => Attitude(observer, other) < 0;

    internal IReadOnlyList<int> CaptureReactions() => _reactions.ToArray();
    internal IReadOnlyList<int> CaptureAttitudes() => _attitudes.ToArray();

    internal static AiStrategicState Initialize(MatchSetup setup, DeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(random);
        var reactions = new int[MatchLimits.PlayerCount];
        var attitudes = new int[MatchLimits.PlayerCount * MatchLimits.PlayerCount];
        if (setup.AiMentality == AiDifficulty.HomicidalManiac)
        {
            for (var observer = 0; observer < MatchLimits.PlayerCount; observer++)
            for (var other = 0; other < MatchLimits.PlayerCount; other++)
            {
                var human = other < setup.Players.Count
                    && setup.Players[other].Controller == PlayerController.Human;
                attitudes[observer * MatchLimits.PlayerCount + other] =
                    human ? MinimumAttitude : MaximumAttitude;
            }
        }
        else
        {
            for (var player = 0; player < MatchLimits.PlayerCount; player++)
                reactions[player] = random.NextInclusive(4) + 2;
        }
        return new AiStrategicState(reactions, attitudes);
    }

    internal static AiStrategicState Restore(
        IReadOnlyList<int> reactions,
        IReadOnlyList<int> attitudes) => new(reactions, attitudes);

    // Recreation saves predating this state still contain the original pre-city
    // random stream. Replaying only its reaction prefix reconstructs the values
    // without advancing the saved live RNG.
    internal static AiStrategicState MigrateLegacy(MatchSetup setup)
    {
        var random = new DeterministicRandom(setup.InitialSeed);
        return Initialize(setup, random);
    }

    internal void RecoverForResolution()
    {
        for (var index = 0; index < _attitudes.Length; index++)
            if (_attitudes[index] < MaximumAttitude) _attitudes[index]++;
    }

    internal void RecordCombat(PlayerId attacker, PlayerId defender, int damage)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        Decrease(defender, attacker, Math.Max(Reaction(defender), damage));
    }

    internal void RecordControl(PlayerId previousOwner, PlayerId newOwner) =>
        Decrease(previousOwner, newOwner, checked(2 * Reaction(previousOwner)));

    private void Decrease(PlayerId observer, PlayerId other, int amount)
    {
        var index = MatrixIndex(observer, other);
        _attitudes[index] = Math.Max(MinimumAttitude, _attitudes[index] - amount);
    }

    private static int MatrixIndex(PlayerId observer, PlayerId other) =>
        checked(PlayerIndex(observer) * MatchLimits.PlayerCount + PlayerIndex(other));

    private static int PlayerIndex(PlayerId player)
    {
        if (player.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
        return player.Value;
    }
}
