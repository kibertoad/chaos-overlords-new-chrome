using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SiteSearchUiTests
{
    [Fact]
    public void SelectionIsIndependentPerPlayerAndResettable()
    {
        var selections = new SiteSearchSelectionState();
        var first = new PlayerId(0);
        var second = new PlayerId(1);

        selections.Toggle(first, 4);
        selections.SelectAll(second, [4, 8, 12]);

        Assert.True(selections.IsSelected(first, 4));
        Assert.False(selections.IsSelected(first, 8));
        Assert.Equal<short>([4, 8, 12], selections.For(second).Order());

        selections.Clear(second);
        Assert.Empty(selections.For(second));
        Assert.True(selections.IsSelected(first, 4));

        selections.Reset();
        Assert.Empty(selections.For(first));
        Assert.Empty(selections.For(second));
    }

    [Fact]
    public void ProjectionAlwaysShowsControlledSitesAndSelectedOtherTypesInCompactedSlots()
    {
        var state = CreateMatch();
        var player = new PlayerId(0);
        var controlled = state.Sectors[0].Sites[0];
        controlled.Resistance = 0;
        controlled.InfluencedBy = player;
        var selected = state.Sectors.SelectMany(sector => sector.Sites)
            .First(site => site != controlled && site.DefinitionId != controlled.DefinitionId);

        var result = CitySiteMarkerProjection.Project(
            state, player, new HashSet<short> { selected.DefinitionId });

        Assert.Contains(result, marker => marker.SiteDefinitionId == controlled.DefinitionId
            && marker.Controlled);
        Assert.Contains(result, marker => marker.SiteDefinitionId == selected.DefinitionId
            && !marker.Controlled);
        Assert.DoesNotContain(result, marker => !marker.Controlled
            && marker.SiteDefinitionId != selected.DefinitionId);
        Assert.Equal(result.OrderBy(marker => marker.SectorId)
            .ThenBy(marker => marker.VisibleSlot), result);
        Assert.All(result.GroupBy(marker => marker.SectorId), markers =>
            Assert.Equal(Enumerable.Range(0, markers.Count()),
                markers.Select(marker => marker.VisibleSlot)));
    }

    [Fact]
    public void DefaultSelectionProjectsOnlyControlledHeadquarters()
    {
        var state = CreateMatch();
        var player = new PlayerId(0);
        var selections = new SiteSearchSelectionState();
        var headquarters = state.Sectors
            .Where(sector => sector.Owner == player)
            .SelectMany(sector => sector.Sites)
            .Single(site => site.Resistance == 0);

        var result = CitySiteMarkerProjection.Project(state, player, selections.For(player));

        var marker = Assert.Single(result);
        Assert.Equal(headquarters.DefinitionId, marker.SiteDefinitionId);
        Assert.True(marker.Controlled);
    }

    [Theory]
    [InlineData(0, true, 0, 0)]
    [InlineData(10, false, 200, 28)]
    [InlineData(11, true, 0, 14)]
    [InlineData(21, false, 200, 42)]
    public void MarkerSourceMatchesRecoveredPx00150Layout(
        short definitionId, bool controlled, int x, int y)
    {
        var marker = new CitySiteMarker(0, definitionId, 0, controlled);

        Assert.Equal(new Rectangle(x, y, 20, 14), CitySiteMarkerProjection.Source(marker));
    }

    [Theory]
    [InlineData(0, 0, 11, 51)]
    [InlineData(63, 2, 382, 438)]
    public void MarkerDestinationMatchesRecoveredCityRenderer(
        int sectorId, int visibleSlot, int x, int y)
    {
        var marker = new CitySiteMarker(sectorId, 0, visibleSlot, true);

        Assert.Equal(new Rectangle(x, y, 20, 14),
            CitySiteMarkerProjection.Destination(marker));
    }

    private static MatchState CreateMatch() => OriginalMatchFactory.Create(
        BundledOriginalData.Load(),
        new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996,
        [
            new MatchPlayerSetup(new PlayerId(0), "PLAYER 1", PlayerController.Human, 0)
        ], allowSparsePlayerIds: true));
}
