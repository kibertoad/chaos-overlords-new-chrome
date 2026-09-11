using System.Text.Json;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The match configuration the host chose, carried in the server's opaque <c>gameSettings</c> blob.
/// </summary>
/// <remarks>
/// <para>
/// The server stores this verbatim and never looks inside: it holds no game rules, so a scenario
/// and a difficulty mean nothing to it. What it does is make sure every client bootstraps the same
/// city — the seed alone is not enough, because the generator also reads the scenario, the duration
/// and the seated players.
/// </para>
/// <para>
/// Player names are part of it in spirit and not in fact: they come from the roster, because the
/// game's own rules read them (<c>OriginalSetupNameRules</c> grants starting cash by name, and
/// <c>OriginalHireCheatRules</c> reads one too), so a client that invented its own would diverge on
/// the first turn.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var settings = new MultiplayerGameSettings(ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal, portraits);
/// var request = new CreateMatchRequest(new MatchSettings(name, 4, 0, MatchVisibility.Private, settings.ToWire()), "Ada", null);
/// </code>
/// </example>
public sealed record MultiplayerGameSettings(
    ScenarioId Scenario,
    GameDuration Duration,
    AiDifficulty AiMentality,
    IReadOnlyList<short> Portraits)
{
    /// <summary>The blob the host sends and every client reads back.</summary>
    public IReadOnlyDictionary<string, JsonElement> ToWire() =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new Wire(
                (int)Scenario, (int)Duration, (int)AiMentality, [.. Portraits]),
                WireJson.Options),
            WireJson.Options)!;

    /// <summary>
    /// The settings inside a blob the server relayed.
    /// </summary>
    /// <remarks>
    /// Every field is validated against the enum it names. The blob is opaque to the server, so
    /// this is the first place a value out of range can be caught, and adopting one would mean
    /// generating a city from a scenario the game does not have.
    /// </remarks>
    public static MultiplayerGameSettings FromWire(IReadOnlyDictionary<string, JsonElement> blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        var wire = JsonSerializer.Deserialize<Wire>(
            JsonSerializer.Serialize(blob, WireJson.Options), WireJson.Options)
            ?? throw new MultiplayerProtocolException("the match carries no game settings");
        return new MultiplayerGameSettings(
            Defined<ScenarioId>(wire.Scenario, nameof(wire.Scenario)),
            Defined<GameDuration>(wire.Duration, nameof(wire.Duration)),
            Defined<AiDifficulty>(wire.AiMentality, nameof(wire.AiMentality)),
            ValidPortraits(wire.Portraits));
    }

    private static TEnum Defined<TEnum>(int value, string field) where TEnum : struct, Enum
    {
        var candidate = (TEnum)Enum.ToObject(typeof(TEnum), value);
        if (!Enum.IsDefined(candidate))
        {
            throw new MultiplayerProtocolException(
                $"the match's {field} is {value}, which this build of the game does not have");
        }
        return candidate;
    }

    private static IReadOnlyList<short> ValidPortraits(IReadOnlyList<short> portraits)
    {
        if (portraits.Count != MatchLimits.PlayerCount
            || portraits.Any(portrait => portrait is < 0 or >= PortraitCount))
        {
            throw new MultiplayerProtocolException(
                $"the match's portraits are not {MatchLimits.PlayerCount} entries of the original atlas");
        }
        return portraits;
    }

    /// <summary>The original overlord portrait atlas holds sixteen faces.</summary>
    private const short PortraitCount = 16;

    /// <summary>
    /// Enums travel as their numeric value, not their name.
    /// </summary>
    /// <remarks>
    /// A name would read better in a database row and is the wrong choice here: renaming a C# enum
    /// member is a refactor nobody expects to break a lobby, and the numeric value is what the save
    /// format already carries.
    /// </remarks>
    private sealed record Wire(int Scenario, int Duration, int AiMentality, short[] Portraits);
}
