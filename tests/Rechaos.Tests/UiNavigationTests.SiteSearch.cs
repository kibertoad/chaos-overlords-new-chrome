using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class UiNavigationTests
{
    [Fact]
    public void SiteSearchPanelFitsAllTwentyTwoSiteTypesInTwoColumns()
    {
        var rows = Enumerable.Range(0, SiteSearchLayout.MaximumSites)
            .Select(SiteSearchLayout.Site).ToArray();

        Assert.Equal(new Rectangle(206, 146, 114, 15), rows[0]);
        Assert.Equal(new Rectangle(322, 296, 114, 15), rows[^1]);
        Assert.All(rows.SelectMany((left, index) => rows.Skip(index + 1)
            .Select(right => (left, right))), pair => Assert.False(pair.left.Intersects(pair.right)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SiteSearchLayout.Site(22));
    }

    [Fact]
    public void SiteSearchHeldFacesFollowScrSearch001()
    {
        // SCR-SEARCH-001, FND-UI-062: the lit faces while ALL, NONE and Done are held.
        Assert.Equal(new Rectangle(137, 140, 50, 23), SiteSearchLayout.HeldFace(SiteSearchControl.All));
        Assert.Equal(new Rectangle(137, 172, 50, 23), SiteSearchLayout.HeldFace(SiteSearchControl.None));
        Assert.Equal(new Rectangle(137, 293, 50, 23), SiteSearchLayout.HeldFace(SiteSearchControl.Done));
        Assert.Equal(new Rectangle(97, 560, 50, 23),
            HeldButtonFaces.Lit(SiteSearchLayout.HeldKind(SiteSearchControl.All)));
        Assert.Equal(new Rectangle(197, 560, 50, 23),
            HeldButtonFaces.Lit(SiteSearchLayout.HeldKind(SiteSearchControl.None)));
        Assert.Equal(new Rectangle(0, 386, 50, 23),
            HeldButtonFaces.Lit(SiteSearchLayout.HeldKind(SiteSearchControl.Done)));
    }

    // FND-SEARCH-004: the second press of a double-click on a row opens Site Information and leaves
    // the row as the first press set it; a press on another control between the two presses makes
    // the second a plain press that flips the row back.
    [Fact]
    public void SiteSearchDoubleClickOpensDetailsOnlyForTwoPressesInARow()
    {
        var rows = Enumerable.Range(0, SiteSearchLayout.MaximumSites).Select(row => (short)row).ToArray();
        var player = new PlayerId(0);
        var row = SiteSearchLayout.Site(5).Center;
        var none = SiteSearchLayout.None.Center;

        var selections = new SiteSearchSelectionState();
        var clicks = new IndexedDoubleClickTracker();
        Assert.False(SiteSearchPanel.Press(selections, player, row, rows, clicks, TimeSpan.FromMilliseconds(100)).OpensDetails);
        Assert.True(SiteSearchPanel.Press(selections, player, row, rows, clicks, TimeSpan.FromMilliseconds(200)).OpensDetails);
        Assert.True(selections.IsSelected(player, 5));

        selections = new SiteSearchSelectionState();
        clicks = new IndexedDoubleClickTracker();
        SiteSearchPanel.Press(selections, player, row, rows, clicks, TimeSpan.FromMilliseconds(100));
        Assert.Equal(SiteSearchControl.None,
            SiteSearchPanel.Press(selections, player, none, rows, clicks, TimeSpan.FromMilliseconds(200)).Press.Control);
        // FND-UI-062: the press on NONE only holds the face; its release inside clears the filter.
        Assert.True(selections.IsSelected(player, 5));
        Assert.False(SiteSearchPanel.Release(selections, player, SiteSearchControl.None, rows));
        Assert.False(selections.IsSelected(player, 5));
        Assert.False(SiteSearchPanel.Press(selections, player, row, rows, clicks, TimeSpan.FromMilliseconds(300)).OpensDetails);
        Assert.True(selections.IsSelected(player, 5));

        Assert.Throws<ArgumentOutOfRangeException>(() => SiteSearchPanel.Apply(
            selections, player, new SiteSearchPress(SiteSearchControl.Row), rows));
    }
}
