using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class UiNavigationTests
{
    [Fact]
    public void SiteSearchHeldFacesFollowScrSearch001()
    {
        // SCR-SEARCH-001, FND-UI-062: the lit faces while ALL, NONE and Done are held.
        Assert.Equal(new Rectangle(137, 140, 50, 23), HeldButtonFaces.Drawn(
            SiteSearchLayout.HeldKind(SiteSearchControl.All), SiteSearchLayout.Target(SiteSearchControl.All), true).Destination);
        Assert.Equal(new Rectangle(137, 172, 50, 23), HeldButtonFaces.Drawn(
            SiteSearchLayout.HeldKind(SiteSearchControl.None), SiteSearchLayout.Target(SiteSearchControl.None), true).Destination);
        Assert.Equal(new Rectangle(137, 293, 50, 23), HeldButtonFaces.Drawn(
            SiteSearchLayout.HeldKind(SiteSearchControl.Done), SiteSearchLayout.Target(SiteSearchControl.Done), true).Destination);
        Assert.Equal(new Rectangle(97, 560, 50, 23),
            HeldButtonFaces.Lit(SiteSearchLayout.HeldKind(SiteSearchControl.All)));
        Assert.Equal(new Rectangle(197, 560, 50, 23),
            HeldButtonFaces.Lit(SiteSearchLayout.HeldKind(SiteSearchControl.None)));
        Assert.Equal(new Rectangle(0, 386, 50, 23),
            HeldButtonFaces.Lit(SiteSearchLayout.HeldKind(SiteSearchControl.Done)));
    }
}
