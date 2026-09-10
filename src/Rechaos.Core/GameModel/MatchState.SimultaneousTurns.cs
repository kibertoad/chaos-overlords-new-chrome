namespace Rechaos.Core.GameModel;

/// <summary>
/// The parts of a match that only a simultaneous turn needs.
/// </summary>
/// <remarks>
/// Hot-seat play walks the seats one at a time, so every step has exactly one active player to
/// attribute itself to. Online play does not: everyone plans at once, and the steps that are
/// shared rather than personal have to be taken in one ordered pass by every client alike.
/// </remarks>
public sealed partial class MatchState
{
    /// <summary>
    /// Draws every seated player's hire offers at once, in slot order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hot-seat play draws a player's offers while that player is the active one, and lazily, when
    /// the dock is opened. A simultaneous turn can do neither: every player plans at the same time,
    /// so only one of them is ever the active one, and drawing spends the shared PRNG, so a draw
    /// that depends on whether somebody opened a panel would put two clients on different streams.
    /// </para>
    /// <para>
    /// This is the simultaneous form: one ordered pass over the seats, taken by every client at the
    /// same point in the turn, so the offers a player plans against are the offers the sealed turn
    /// grants. Returns the players it drew for, which is what the replay records.
    /// </para>
    /// </remarks>
    public IReadOnlyList<PlayerId> PrepareSimultaneousHireOffers()
    {
        if (Coordinator.Phase != TurnPhase.Command)
            throw new InvalidOperationException("Simultaneous hire offers are drawn during Command.");
        var drawn = new List<PlayerId>();
        foreach (var player in Players.OrderBy(candidate => candidate.Id.Value))
        {
            if (player.Status != PlayerStatus.Active) continue;
            // The same condition the single-player dock applies: a seat with a hire already queued,
            // or one that has snubbed this turn, has spent its action and is not refilled.
            if (player.HireOfferSlots.All(slot => slot.GangDefinitionId.HasValue)
                || player.PendingHires.Count > 0
                || player.HasSnubbedHireOfferThisTurn)
            {
                continue;
            }
            HireResolver.FillOffers(this, player);
            drawn.Add(player.Id);
        }
        return drawn;
    }
}
