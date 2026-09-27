using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SeatPlanningPresentationTests
{
    private static readonly IReadOnlySet<int> BothHumanSeats = new HashSet<int> { 0, 2 };

    [Fact]
    public void AnOpponentWhoHasNotCommittedTheTurnIsStillDrafting() =>
        Assert.True(SeatPlanningPresentation.IsDrafting(
            slot: 2, ownSlot: 0, turnIsOpen: true, ownTurnSent: false, BothHumanSeats, new HashSet<int>()));

    [Fact]
    public void AnOpponentWhoHasCommittedTheTurnIsNotDrafting() =>
        Assert.False(SeatPlanningPresentation.IsDrafting(
            slot: 2, ownSlot: 0, turnIsOpen: true, ownTurnSent: false, BothHumanSeats, new HashSet<int> { 2 }));

    /// <summary>The player's own seat is marked while the turn is still theirs to end.</summary>
    [Fact]
    public void TheOwnSeatIsMarkedUntilTheTurnIsSent() =>
        Assert.True(SeatPlanningPresentation.IsDrafting(
            slot: 0, ownSlot: 0, turnIsOpen: true, ownTurnSent: false, BothHumanSeats,
            new HashSet<int>()));

    /// <summary>
    /// The mark leaves the player's own seat as soon as they end the turn.
    /// </summary>
    /// <remarks>
    /// Before the server's readiness echo arrives: the player just pressed the button, and a WAIT
    /// that outlived it would read as the press not having registered.
    /// </remarks>
    [Fact]
    public void TheOwnSeatIsNotMarkedOnceTheTurnIsSent() =>
        Assert.False(SeatPlanningPresentation.IsDrafting(
            slot: 0, ownSlot: 0, turnIsOpen: true, ownTurnSent: true, BothHumanSeats,
            new HashSet<int>()));

    /// <summary>
    /// The player's own seat follows what this client did, not the server's readiness roster.
    /// </summary>
    [Fact]
    public void TheOwnSeatIgnoresTheReadinessRoster() =>
        Assert.True(SeatPlanningPresentation.IsDrafting(
            slot: 0, ownSlot: 0, turnIsOpen: true, ownTurnSent: false, BothHumanSeats,
            new HashSet<int> { 0 }));

    /// <summary>An own seat the table handed to the computer is not waited on either.</summary>
    [Fact]
    public void AnOwnSeatTheTurnDoesNotWaitOnIsNotMarked() =>
        Assert.False(SeatPlanningPresentation.IsDrafting(
            slot: 0, ownSlot: 0, turnIsOpen: true, ownTurnSent: false, new HashSet<int> { 2 },
            new HashSet<int>()));

    /// <summary>A seat the turn does not seal against is nobody the match is waiting for.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void ASeatTheTurnDoesNotWaitOnIsNotMarked(int slot) =>
        Assert.False(SeatPlanningPresentation.IsDrafting(
            slot, ownSlot: 0, turnIsOpen: true, ownTurnSent: false, BothHumanSeats, new HashSet<int>()));

    /// <summary>Nobody is drafting while the match is paused, over or not yet playing.</summary>
    [Fact]
    public void AClosedTurnMarksNobody() =>
        Assert.False(SeatPlanningPresentation.IsDrafting(
            slot: 2, ownSlot: 0, turnIsOpen: false, ownTurnSent: false, BothHumanSeats, new HashSet<int>()));
}
