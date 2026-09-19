using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class InfluenceCommandLayoutTests
{
    [Fact]
    public void SiteSelectionUsesRecoveredTargetsRatherThanArtworkApertures()
    {
        Assert.Equal(new Rectangle(209, 140, 120, 64), InfluenceCommandLayout.Site(0));
        Assert.Equal(new Rectangle(312, 197, 120, 64), InfluenceCommandLayout.Site(1));
        Assert.Equal(new Rectangle(209, 254, 120, 64), InfluenceCommandLayout.Site(2));

        Assert.Equal(new Rectangle(210, 141, 120, 64), InfluenceCommandLayout.SiteHit(0));
        Assert.Equal(new Rectangle(312, 197, 120, 64), InfluenceCommandLayout.SiteHit(1));
        Assert.Equal(new Rectangle(210, 251, 120, 64), InfluenceCommandLayout.SiteHit(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => InfluenceCommandLayout.SiteHit(3));
    }
}
