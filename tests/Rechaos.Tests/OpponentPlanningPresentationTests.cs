using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OpponentPlanningPresentationTests
{
    private static readonly IReadOnlySet<int> BothHumanSeats = new HashSet<int> { 0, 2 };

    [Fact]
    public void AnOpponentWhoHasNotCommittedTheTurnIsStillDrafting() =>
        Assert.True(OpponentPlanningPresentation.IsDrafting(
            slot: 2, ownSlot: 0, turnIsOpen: true, BothHumanSeats, new HashSet<int>()));

    [Fact]
    public void AnOpponentWhoHasCommittedTheTurnIsNotDrafting() =>
        Assert.False(OpponentPlanningPresentation.IsDrafting(
            slot: 2, ownSlot: 0, turnIsOpen: true, BothHumanSeats, new HashSet<int> { 2 }));

    /// <summary>
    /// The player's own seat never carries the caption.
    /// </summary>
    /// <remarks>
    /// They know whether they have committed their turn, and the footer says so besides; a WAIT
    /// under their own portrait would read as the game waiting on somebody else.
    /// </remarks>
    [Fact]
    public void TheOwnSeatIsNeverMarked() =>
        Assert.False(OpponentPlanningPresentation.IsDrafting(
            slot: 0, ownSlot: 0, turnIsOpen: true, BothHumanSeats, new HashSet<int>()));

    /// <summary>A seat the turn does not seal against is nobody the match is waiting for.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void ASeatTheTurnDoesNotWaitOnIsNotMarked(int slot) =>
        Assert.False(OpponentPlanningPresentation.IsDrafting(
            slot, ownSlot: 0, turnIsOpen: true, BothHumanSeats, new HashSet<int>()));

    /// <summary>Nobody is drafting while the match is paused, over or not yet playing.</summary>
    [Fact]
    public void AClosedTurnMarksNobody() =>
        Assert.False(OpponentPlanningPresentation.IsDrafting(
            slot: 2, ownSlot: 0, turnIsOpen: false, BothHumanSeats, new HashSet<int>()));

    /// <summary>
    /// The caption sits centred in the gap between a portrait and the top of the map.
    /// </summary>
    /// <remarks>
    /// Its bottom edge is the map's own top at y 44: any lower and the top bar would draw over
    /// the city, and off-centre it would drift under the neighbouring portrait's column.
    /// </remarks>
    [Fact]
    public void TheWaitCaptionSitsUnderThePortraitItBelongsTo()
    {
        var caption = PlayerPortraitLayout.CityCaption(
            2, OpponentPlanningPresentation.WaitingCaption.Length);

        Assert.Equal(new Rectangle(164, 37, 24, 7), caption);
        Assert.Equal(PlayerPortraitLayout.CityTop(2).Bottom + 1, caption.Y);
        Assert.Equal(CityMapLayout.Top, caption.Bottom);
    }

    [Fact]
    public void ACaptionWithoutCharactersHasNowhereToGo() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPortraitLayout.CityCaption(0, 0));
}
