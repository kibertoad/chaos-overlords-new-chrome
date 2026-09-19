using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// Which opponents the online city screen says are still drafting the open turn.
/// </summary>
/// <remarks>
/// The footer says how many seats the turn is waiting on; this says which of them, under the
/// portraits already on the top bar. A player who has committed their turn and is waiting can then
/// see whether the hold-up is the last opponent or three of them, and which.
/// </remarks>
public static class OpponentPlanningPresentation
{
    /// <summary>What a seat that has not committed its turn is marked with.</summary>
    public const string WaitingCaption = "WAIT";

    /// <summary>Green, which the interface keeps for a state that is expected to change.</summary>
    public static Color WaitingColor => Color.Lime;

    /// <summary>
    /// Whether a seat belongs to an opponent whose turn is still being drafted.
    /// </summary>
    /// <remarks>
    /// The player's own seat never carries the caption: they know whether they have committed their
    /// turn, and the footer tells them anyway. Neither does a seat the turn does not seal
    /// against — a computer empire, a player who left, or one the table has voted onto computer
    /// control — since nothing is waited on there and a caption would say the opposite.
    /// </remarks>
    /// <param name="slot">The seat the caption would be drawn under.</param>
    /// <param name="ownSlot">This client's own seat.</param>
    /// <param name="turnIsOpen">Whether the open turn is still being planned at all.</param>
    /// <param name="awaitedSlots">The seats the turn seals against.</param>
    /// <param name="readySlots">The seats that have committed the open turn.</param>
    public static bool IsDrafting(
        int slot,
        int ownSlot,
        bool turnIsOpen,
        IReadOnlySet<int> awaitedSlots,
        IReadOnlySet<int> readySlots)
    {
        ArgumentNullException.ThrowIfNull(awaitedSlots);
        ArgumentNullException.ThrowIfNull(readySlots);
        return turnIsOpen
            && slot != ownSlot
            && awaitedSlots.Contains(slot)
            && !readySlots.Contains(slot);
    }
}
