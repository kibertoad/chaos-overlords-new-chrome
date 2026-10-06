using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class InfluenceCommandLayoutTests
{
    [Fact]
    public void SitesAreDrawnAndPressedInTheRecoveredRectangles()
    {
        Assert.Equal(new Rectangle(210, 141, 120, 64), InfluenceCommandLayout.SiteHit(0));
        Assert.Equal(new Rectangle(312, 197, 120, 64), InfluenceCommandLayout.SiteHit(1));
        Assert.Equal(new Rectangle(210, 251, 120, 64), InfluenceCommandLayout.SiteHit(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => InfluenceCommandLayout.SiteHit(3));
    }

    [Fact]
    public void TheCancelAndConfirmControlsAreTheSharedPanelFaces()
    {
        // SCR-INFLUENCE-001: Cancel at panel (33,137,49,22) and the confirmation control at
        // (33,169,49,22), drawn with the faces the other command panels share (FND-UI-019).
        Assert.Equal(CommandPanelFaces.CancelHit, InfluenceCommandLayout.Cancel);
        Assert.Equal(CommandPanelFaces.ConfirmHit, InfluenceCommandLayout.Ok);
        Assert.Equal(new Rectangle(137, 293, 49, 22), InfluenceCommandLayout.Ok);
    }
}
