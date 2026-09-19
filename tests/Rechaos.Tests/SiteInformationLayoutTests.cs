using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SiteInformationLayoutTests
{
    [Fact]
    public void UsesTheRecoveredAlternatePanelGeometry()
    {
        Assert.Equal(new Rectangle(128, 124, 320, 209), SiteInformationLayout.Panel);
        Assert.Equal(new Rectangle(0, 0, 320, 209), SiteInformationLayout.BackgroundSource);
        Assert.Equal(new Rectangle(156, 139, 120, 64), SiteInformationLayout.Portrait);
        Assert.Equal(new Rectangle(161, 293, 49, 22), SiteInformationLayout.Ok);
        Assert.Equal(288, SiteInformationLayout.NameLeft);
        Assert.Equal(396, SiteInformationLayout.DataValueLeft);
        Assert.Equal(300, SiteInformationLayout.LeftValueLeft);
        Assert.Equal(396, SiteInformationLayout.RightValueLeft);
        Assert.Equal([169, 187, 196, 205], Enumerable.Range(0, 4)
            .Select(SiteInformationLayout.DataY));
        Assert.Equal([244, 253, 271, 280, 289, 298, 307], Enumerable.Range(0, 7)
            .Select(SiteInformationLayout.StatisticY));
    }
}
