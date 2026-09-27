namespace Rechaos.Game;

/// <summary>
/// Which seats an online turn still waits on. The Overlord bar lights a seat's planning light
/// while it does (FND-UI-017).
/// </summary>
public static class SeatPlanningPresentation
{
    /// <summary>
    /// Whether a seat's turn is still being drafted.
    /// </summary>
    /// <remarks>
    /// The player's own seat is judged by what this client did rather than by the server's
    /// readiness roster: the light has to go the moment they end their turn, not a round trip later,
    /// and it has to stay while the turn is still theirs to plan whatever an echo says. No seat the
    /// turn does not seal against is drafting: a computer empire, a player who left, or one the
    /// table has voted onto computer control, the player's own included.
    /// </remarks>
    /// <param name="slot">The seat asked about.</param>
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
