using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

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
                ? new MatchPlayerSetup(
                    new PlayerId(slot), SeatName(player, slot), PlayerController.Human, portrait)
                : new MatchPlayerSetup(
                    new PlayerId(slot), DerivedSeatName(slot), PlayerController.Computer, portrait);
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

    /// <summary>
    /// Every player that holds a seat, by slot — whatever their status is now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Status is deliberately not read. A slot is assigned once, when the host starts the match, and
    /// never reassigned, so "has a slot" means "was seated at the start" and says the same thing
    /// whenever it is asked. Filtering on a status that changes over the life of a match would make
    /// the generated city depend on <em>when</em> a client bootstrapped it: a player who has since
    /// left would seat a computer player under a derived name on the late client and a human under
    /// their own name on the early ones, and because the rules read player names — starting cash is
    /// granted by one — the two would disagree from the first upkeep.
    /// </para>
    /// <para>
    /// Whether a seat is still <em>played</em> by its human is a separate question, answered per
    /// turn by the sealed set rather than by the setup: see <see cref="SealedTurnApplier"/>.
    /// </para>
    /// </remarks>
    private static Dictionary<int, PlayerView> SeatedBySlot(IReadOnlyList<PlayerView> players)
    {
        var seated = new Dictionary<int, PlayerView>();
        foreach (var player in players)
        {
            // A player still in the lobby has slot -1, and seating one would put two players in the
            // same chair. A slot past the board is a server this client cannot play against.
            if (player.Slot is < 0 or >= MatchLimits.PlayerCount) continue;
            if (!seated.TryAdd(player.Slot, player))
            {
                throw new MultiplayerProtocolException(
                    $"the roster seats two players in slot {player.Slot}");
            }
        }
        return seated;
    }

    /// <summary>
    /// The name a seated player's overlord plays under.
    /// </summary>
    /// <remarks>
    /// Their own, unless it is one the original rules read as a cheat code rather than as a name —
    /// see <see cref="ReservedPlayerNames"/>. The lobby refuses those outright, so this is the
    /// safety net for a server that did not: substituting the seat's derived name is deterministic,
    /// so every client substitutes the same one and the match stays in step while staying fair.
    /// </remarks>
    private static string SeatName(PlayerView player, int slot) =>
        ReservedPlayerNames.IsReserved(player.DisplayName)
            ? DerivedSeatName(slot)
            : player.DisplayName;

    /// <summary>
    /// The name a seat plays under when it has no name of its own.
    /// </summary>
    /// <remarks>
    /// It has to be derived, not chosen: the game's own rules read player names — starting cash is
    /// granted by name and one of the recovered hire behaviours keys off one — so two clients that
    /// named the same empty seat differently would diverge on the first upkeep.
    /// </remarks>
    private static string DerivedSeatName(int slot) => $"PLAYER {slot + 1}";
}
