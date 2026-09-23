using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// Which seats the online city screen says are still drafting the open turn.
/// </summary>
/// <remarks>
/// The footer says how many seats the turn is waiting on; this says which of them, under the
/// portraits already on the top bar. A player who has committed their turn and is waiting can then
/// see whether the hold-up is the last opponent or three of them, and which — and a player who has
/// not sees the same mark under their own portrait, so the one question the footer's count leaves
/// open, whether the turn is waiting on them, is answered where they are already looking.
/// </remarks>
public static class OpponentPlanningPresentation
{
    /// <summary>What a seat that has not committed its turn is marked with.</summary>
    public const string WaitingCaption = "WAIT";

    /// <summary>Green, which the interface keeps for a state that is expected to change.</summary>
    public static Color WaitingColor => Color.Lime;

    /// <summary>
    /// Whether a seat's turn is still being drafted.
    /// </summary>
    /// <remarks>
    /// The player's own seat is judged by what this client did rather than by the server's
    /// readiness roster: the mark has to go the moment they end their turn, not a round trip later,
    /// and it has to stay while the turn is still theirs to plan whatever an echo says. No seat the
    /// turn does not seal against carries the caption — a computer empire, a player who left, or
    /// one the table has voted onto computer control, the player's own included — since nothing is
    /// waited on there and a caption would say the opposite.
    /// </remarks>
    /// <param name="slot">The seat the caption would be drawn under.</param>
    /// <param name="ownSlot">This client's own seat.</param>
    /// <param name="turnIsOpen">Whether the open turn is still being planned at all.</param>
    /// <param name="ownTurnSent">Whether this client has ended its own turn.</param>
    /// <param name="awaitedSlots">The seats the turn seals against.</param>
    /// <param name="readySlots">The seats that have committed the open turn.</param>
    public static bool IsDrafting(
        int slot,
        int ownSlot,
        bool turnIsOpen,
        bool ownTurnSent,
        IReadOnlySet<int> awaitedSlots,
        IReadOnlySet<int> readySlots)
    {
        ArgumentNullException.ThrowIfNull(awaitedSlots);
        ArgumentNullException.ThrowIfNull(readySlots);
        if (!turnIsOpen || !awaitedSlots.Contains(slot)) return false;
        return slot == ownSlot ? !ownTurnSent : !readySlots.Contains(slot);
    }
}
