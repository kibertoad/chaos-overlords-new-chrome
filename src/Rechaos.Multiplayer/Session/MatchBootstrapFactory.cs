using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Builds the same city on every client from what the server settled at match start.
/// </summary>
/// <remarks>
/// The server does not simulate, so this is where a match actually begins: the seed it drew, the
/// settings the host chose, and the seating it froze are the whole input, and the generator is
/// deterministic over them. Anything a client added of its own — a locally chosen name, a portrait
/// it preferred — would be a divergence on turn one.
/// </remarks>
public static class MatchBootstrapFactory
{
    /// <summary>
    /// The setup for a seated match.
    /// </summary>
    /// <param name="seed">
    /// The server's seed. It is a SIGNED 32-bit integer, so half of all matches start on a negative
    /// one; a client that narrowed it would refuse those.
    /// </param>
    /// <param name="settings">The host's choices, from the opaque blob.</param>
    /// <param name="players">The seated roster, in any order; slots decide the seating.</param>
    public static MatchSetup Setup(
        int seed,
        MultiplayerGameSettings settings,
        IReadOnlyList<PlayerView> players)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(players);
        var seated = SeatedBySlot(players);
        var setups = new MatchPlayerSetup[MatchLimits.PlayerCount];
        for (var slot = 0; slot < MatchLimits.PlayerCount; slot++)
        {
            var portrait = settings.Portraits[slot];
            setups[slot] = seated.TryGetValue(slot, out var player)
                ? new MatchPlayerSetup(new PlayerId(slot), player.DisplayName, PlayerController.Human, portrait)
                : new MatchPlayerSetup(new PlayerId(slot), ComputerName(slot), PlayerController.Computer, portrait);
        }
        return new MatchSetup(
            settings.Scenario, settings.Duration, seed, setups, settings.AiMentality);
    }

    /// <summary>The match, generated from the setup, with hire offers drawn for every seat.</summary>
    public static MatchState Create(
        OriginalData definitions,
        int seed,
        MultiplayerGameSettings settings,
        IReadOnlyList<PlayerView> players)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return OriginalMatchFactory.Create(definitions, Setup(seed, settings, players));
    }

    /// <summary>The slots a human is seated in; every other slot is a computer player.</summary>
    public static IReadOnlySet<int> HumanSlots(IReadOnlyList<PlayerView> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        return SeatedBySlot(players).Keys.ToHashSet();
    }

    /// <summary>
    /// A departed player's seat is a computer player's, so only an active roster entry seats a human.
    /// </summary>
    /// <remarks>
    /// A seat is only real once the match has started: a player still in the lobby has slot -1, and
    /// seating one would put two players in the same chair.
    /// </remarks>
    private static Dictionary<int, PlayerView> SeatedBySlot(IReadOnlyList<PlayerView> players)
    {
        var seated = new Dictionary<int, PlayerView>();
        foreach (var player in players)
        {
            if (player.Status != WirePlayerStatus.Active) continue;
            if (player.Slot is < 0 or >= MatchLimits.PlayerCount) continue;
            if (!seated.TryAdd(player.Slot, player))
            {
                throw new MultiplayerProtocolException(
                    $"the roster seats two active players in slot {player.Slot}");
            }
        }
        return seated;
    }

    /// <summary>
    /// The name an unseated slot plays under.
    /// </summary>
    /// <remarks>
    /// It has to be derived, not chosen: the game's own rules read player names — starting cash is
    /// granted by name and one of the recovered hire behaviours keys off one — so two clients that
    /// named the same empty seat differently would diverge on the first upkeep.
    /// </remarks>
    private static string ComputerName(int slot) => $"PLAYER {slot + 1}";
}
