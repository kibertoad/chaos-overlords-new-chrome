using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The positions and hit tests of SCR-UI-003 and SCR-UI-004 against FND-UI-015, FND-UI-017,
/// FND-UI-018 and FND-UI-038.
/// </summary>
public sealed class CitySectorScreenLayoutTests
{
    [Theory]
    [InlineData(2, 42, 0)]
    [InlineData(55, 42, 0)]
    [InlineData(56, 42, 1)]
    [InlineData(2, 93, 0)]
    [InlineData(2, 94, 8)]
    [InlineData(433, 457, 63)]
    public void ACityPressTakesTheSectorOfTheOriginalsFiftyFourByFiftyTwoGrid(int x, int y, int sector)
    {
        Assert.True(CityMapLayout.TrySectorAt(new Point(x, y), out var mapped));
        Assert.Equal(sector, mapped);
    }

    [Fact]
    public void TheCityPressGridDriftsFromTheDrawnGrid()
    {
        // Drawn cell 7 starts at x 6 + 53 * 7 = 377, but a press there still takes column 6.
        Assert.Equal(377, CityMapLayout.Destination(7).X);
        Assert.True(CityMapLayout.TrySectorAt(new Point(377, 60), out var mapped));
        Assert.Equal(6, mapped);
        Assert.False(CityMapLayout.TrySectorAt(new Point(1, 42), out _));
        Assert.False(CityMapLayout.TrySectorAt(new Point(2, 41), out _));
    }

    [Fact]
    public void TheSelectionFrameAlternatesEveryFourPresentationTicks()
    {
        Assert.Equal(new Rectangle(236, 15, 54, 52), CityMapLayout.SelectionFrameSource(0));
        Assert.Equal(new Rectangle(290, 15, 54, 52), CityMapLayout.SelectionFrameSource(1));
        var tick = PresentationClock.Period;
        Assert.Equal(0, CityMapLayout.SelectionFrame(tick * 3));
        Assert.Equal(1, CityMapLayout.SelectionFrame(tick * 4));
        Assert.Equal(1, CityMapLayout.SelectionFrame(tick * 7));
        Assert.Equal(0, CityMapLayout.SelectionFrame(tick * 8));
    }

    [Fact]
    public void TheGridLabelsSitOnTabsAroundTheMap()
    {
        var labels = CityMapLayout.GridLabels().ToArray();

        Assert.Equal(32, labels.Length);
        Assert.Contains(new GridLabel(new Point(21, 42), new Rectangle(276, 448, 23, 13), new Point(9, 1), "A"), labels);
        Assert.Contains(new GridLabel(new Point(392, 444), new Rectangle(276, 461, 23, 13), new Point(9, 5), "H"), labels);
        Assert.Contains(new GridLabel(new Point(3, 59), new Rectangle(299, 448, 13, 23), new Point(1, 8), "1"), labels);
        Assert.Contains(new GridLabel(new Point(421, 416), new Rectangle(312, 448, 13, 23), new Point(7, 8), "8"), labels);
    }

    [Fact]
    public void TheOverlordBarPlacesSeatsSeventyPixelsApart()
    {
        Assert.Equal(new Rectangle(18, 5, 32, 32), OverlordBarLayout.Portrait(0));
        Assert.Equal(new Rectangle(368, 5, 32, 32), OverlordBarLayout.Portrait(5));
        Assert.Equal(new Rectangle(50, 6, 20, 20), OverlordBarLayout.Marker(0));
        Assert.Equal(new Rectangle(120, 5, 20, 20), OverlordBarLayout.MarkerBackground(1));
        Assert.Equal(new Rectangle(51, 30, 20, 6), OverlordBarLayout.PlanningLight(0));
        Assert.Equal(new Rectangle(66, 347, 20, 6), OverlordBarLayout.PlanningLightSource);
        Assert.Equal(new Rectangle(158, 5, 54, 32), OverlordBarLayout.EmptySeat(2));
        Assert.Equal(new Rectangle(458, 448, 54, 32), OverlordBarLayout.EmptySeatSource(2));
        Assert.Equal(new Rectangle(64, 594, 32, 32), OverlordBarLayout.UnseenPortraitSource(2));
        Assert.Equal(new Rectangle(152, 5, 62, 32), OverlordBarLayout.PortraitHit(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverlordBarLayout.Portrait(MatchLimits.PlayerCount));
    }

    [Fact]
    public void AHumanSeatStaysLitUntilItsOrdersAreIn()
    {
        Assert.True(OverlordBarLayout.PlanningLightLit(human: true, ordersIn: false));
        Assert.False(OverlordBarLayout.PlanningLightLit(human: true, ordersIn: true));
        Assert.False(OverlordBarLayout.PlanningLightLit(human: false, ordersIn: false));
    }

    [Fact]
    public void TheEmptySeatArtStepsWithTheMarkerTimer()
    {
        Assert.Equal(0, ActivePlayerMarkerPresentation.EmptySeatFrame(TimeSpan.Zero));
        Assert.Equal(2, ActivePlayerMarkerPresentation.EmptySeatFrame(TimeSpan.FromMilliseconds(250)));
        Assert.Equal(0, ActivePlayerMarkerPresentation.EmptySeatFrame(TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public void TheSectorValuesFollowRuleUi011()
    {
        Assert.Equal(568, StatusConsoleLayout.SectorValueLeft);
        Assert.Equal(["LO", "LM", "MI", "UM", "UP"],
            Enumerable.Range(3, 5).Select(StatusConsolePresentation.IncomeWord));
    }

    [Fact]
    public void TheNineSectorDisplayIsACropOfTheMapAtTheTopLeftNeighbour()
    {
        Assert.Equal(new Rectangle(64, 60, 162, 156), SectorDetailLayout.Display);
        // Sector 27 is column 3, row 3: the crop starts at the cell of sector 18.
        Assert.Equal(new Rectangle(110, 105, 162, 156), SectorDetailLayout.DisplaySource(27));
        Assert.Equal(CityMapLayout.Source(18).Location, SectorDetailLayout.DisplaySource(27).Location);
        Assert.Equal(new Rectangle(64, 60, 54, 52), SectorDetailLayout.Cell(0, 0));
        Assert.Equal(new Rectangle(170, 162, 54, 52), SectorDetailLayout.Cell(2, 2));
        // FND-UI-018: the lightened cell sits at (1 + 53i, 1 + 51j) of the display.
        Assert.Equal(new Rectangle(118, 112, 52, 50),
            TickedPresentation.LitArea(TickedPresentationKind.SectorDisplayCellFlash, SectorDetailLayout.Cell(1, 1)));
        Assert.Equal(new Rectangle(146, 127, 20, 20), SectorDetailLayout.Marker(27, 27));
        Assert.Null(SectorDetailLayout.Marker(27, 29));
    }

    [Fact]
    public void OffMapRowsAndColumnsOfTheDisplayAreBlack()
    {
        Assert.Equal([new Rectangle(64, 60, 162, 51), new Rectangle(64, 60, 53, 156)],
            SectorDetailLayout.OffMapBands(0));
        Assert.Equal([new Rectangle(64, 163, 162, 53), new Rectangle(171, 60, 55, 156)],
            SectorDetailLayout.OffMapBands(63));
        Assert.Empty(SectorDetailLayout.OffMapBands(27));
    }

    [Theory]
    [InlineData(64, 60, 0, 0)]
    [InlineData(117, 111, 0, 0)]
    [InlineData(118, 112, 1, 1)]
    [InlineData(171, 163, 1, 1)]
    [InlineData(172, 164, 2, 2)]
    [InlineData(225, 215, 2, 2)]
    public void ADisplayPressTakesTheOriginalsColumnAndRow(int x, int y, int column, int row)
    {
        Assert.True(SectorDetailLayout.TryCellAt(new Point(x, y), out var mappedColumn, out var mappedRow));
        Assert.Equal((column, row), (mappedColumn, mappedRow));
    }

    [Fact]
    public void ADisplayPressOffTheMapSelectsNothing()
    {
        Assert.False(SectorDetailLayout.TrySectorAt(new Point(70, 70), 0, out _));
        Assert.True(SectorDetailLayout.TrySectorAt(new Point(180, 120), 27, out var right));
        Assert.Equal(28, right);
        Assert.False(SectorDetailLayout.TryCellAt(new Point(226, 100), out _, out _));
    }

    [Fact]
    public void TheDisplayLabelsSitOnTheFrame()
    {
        var labels = SectorDetailLayout.DisplayLabels(27).ToArray();

        Assert.Equal(6, labels.Length);
        Assert.Contains(new GridLabel(new Point(65, 126), new Rectangle(348, 448, 13, 23), new Point(1, 8), "4"), labels);
        Assert.Contains(new GridLabel(new Point(65, 74), new Rectangle(348, 448, 13, 23), new Point(1, 8), "3"), labels);
        Assert.Contains(new GridLabel(new Point(65, 178), new Rectangle(348, 448, 13, 23), new Point(1, 8), "5"), labels);
        Assert.Contains(new GridLabel(new Point(133, 61), new Rectangle(325, 448, 23, 13), new Point(9, 1), "D"), labels);
        Assert.Contains(new GridLabel(new Point(79, 61), new Rectangle(325, 448, 23, 13), new Point(9, 1), "C"), labels);
        Assert.Contains(new GridLabel(new Point(187, 61), new Rectangle(325, 448, 23, 13), new Point(9, 1), "E"), labels);
        // A corner sector has no neighbour above or to its left to label.
        Assert.Equal(4, SectorDetailLayout.DisplayLabels(0).Count());
    }

    [Theory]
    [InlineData(254, 80, 0)]
    [InlineData(329, 192, 0)]
    [InlineData(330, 80, 1)]
    [InlineData(254, 193, 2)]
    [InlineData(403, 305, 5)]
    [InlineData(403, 415, 5)]
    public void ACardPressTakesTheOriginalsCard(int x, int y, int card) =>
        Assert.Equal(card, SectorGangCardLayout.CardAt(new Point(x, y)));

    [Fact]
    public void TheSectorScreenPlacesSitesStripsAndTheBackControl()
    {
        Assert.Equal(-1, SectorGangCardLayout.CardAt(new Point(404, 100)));
        Assert.Equal(new Rectangle(86, 360, 120, 64), SectorDetailLayout.SitePortrait(2));
        Assert.Equal(new Rectangle(96, 419, 100, 3), SectorDetailLayout.SiteControlBar(2));
        Assert.Equal(0, SectorDetailLayout.SiteAt(new Point(86, 294)));
        Assert.Equal(1, SectorDetailLayout.SiteAt(new Point(100, 295)));
        Assert.Equal(2, SectorDetailLayout.SiteAt(new Point(205, 423)));
        Assert.Equal(-1, SectorDetailLayout.SiteAt(new Point(206, 300)));
        Assert.Equal(new Rectangle(4, 394, 32, 63), SectorDetailLayout.Back);
        Assert.Equal(new Rectangle(236, 67, 32, 207), SectorDetailLayout.OwnerStripSource(null));
        Assert.Equal(new Rectangle(300, 67, 32, 207), SectorDetailLayout.OwnerStripSource(new PlayerId(1)));
        Assert.Equal(new Rectangle(460, 67, 32, 207), SectorDetailLayout.BackStripSource);
        Assert.Equal(0, SectorDetailLayout.SiteControlWidth(10, 10));
        Assert.Equal(30, SectorDetailLayout.SiteControlWidth(10, 7));
        Assert.Equal(100, SectorDetailLayout.SiteControlWidth(0, 0));
        Assert.Equal(new Rectangle(354, 0, 30, 3), SectorDetailLayout.SiteControlBarSource(30));
    }

    [Fact]
    public void EveryRegionOfEveryCardHasItsOwnClickKey()
    {
        var keys = Enumerable.Range(0, 40)
            .SelectMany(gang => Enumerable.Range(0, SectorGangCardLayout.ItemSlots + 1)
                .Select(region => SectorGangCardLayout.ClickKey(new GangId(gang), region)))
            .ToArray();
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SectorGangCardLayout.ClickKey(new GangId(1), SectorGangCardLayout.ItemSlots + 1));
    }

    [Fact]
    public void AnEquipmentSlotIsFoundUnderThePoint()
    {
        Assert.Equal(0, SectorGangCardLayout.ItemSlotAt(3, SectorGangCardLayout.ItemSlot(3, 0).Center));
        Assert.Equal(2, SectorGangCardLayout.ItemSlotAt(3, SectorGangCardLayout.ItemSlot(3, 2).Center));
        Assert.Equal(-1, SectorGangCardLayout.ItemSlotAt(3, SectorGangCardLayout.Portrait(3).Center));
    }

    [Fact]
    public void TheSectorViewSetsTheMarkerBackToItsFirstFrame()
    {
        // FND-UI-018, FND-UI-038: composing a sector view resets the marker counter, which then
        // steps on the same 100 ms ticks as before.
        var clock = new ActivePlayerMarkerClock();
        Assert.Equal(5, clock.Frame(TimeSpan.FromMilliseconds(530)));
        clock.SectorView(27, new PlayerId(0), TimeSpan.FromMilliseconds(530));
        Assert.Equal(0, clock.Frame(TimeSpan.FromMilliseconds(599)));
        Assert.Equal(1, clock.Frame(TimeSpan.FromMilliseconds(600)));
        // The same view drawn again is not a new composition.
        clock.SectorView(27, new PlayerId(0), TimeSpan.FromMilliseconds(700));
        Assert.Equal(2, clock.Frame(TimeSpan.FromMilliseconds(700)));
        // Another player's cards, another sector, or a return from the city are.
        clock.SectorView(27, new PlayerId(1), TimeSpan.FromMilliseconds(800));
        Assert.Equal(0, clock.Frame(TimeSpan.FromMilliseconds(800)));
        clock.SectorView(28, new PlayerId(1), TimeSpan.FromMilliseconds(1000));
        Assert.Equal(0, clock.Frame(TimeSpan.FromMilliseconds(1000)));
        clock.OtherView();
        Assert.Equal(3, clock.Frame(TimeSpan.FromMilliseconds(1300)));
        clock.SectorView(28, new PlayerId(1), TimeSpan.FromMilliseconds(1300));
        Assert.Equal(0, clock.Frame(TimeSpan.FromMilliseconds(1300)));
    }
}
