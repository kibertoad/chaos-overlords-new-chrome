using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SiteSpecialTooltipTests
{
    private static readonly Point Portrait = SiteInformationLayout.Portrait.Center;

    [Fact]
    public void OrdinarySitePortraitHasNoTooltip() =>
        Assert.Empty(InformationEffectTooltips.SiteAt(Portrait));

    [Fact]
    public void FactoryPortraitExplainsTheEquipmentDiscount()
    {
        var lines = InformationEffectTooltips.SiteAt(Portrait, SpecialSiteRules.Factory);

        Assert.Equal("FACTORY", lines[0]);
        Assert.Contains("EQUIPMENT", string.Join(' ', lines));
        Assert.Contains("1/3 LESS", string.Join(' ', lines));
    }

    [Theory]
    [InlineData(SpecialSiteRules.ScienceCenter, "SCIENCE CENTER", "TECH 8 INSTEAD OF 5")]
    [InlineData(SpecialSiteRules.ResearchLab, "RESEARCH LAB", "TECH 10 INSTEAD OF 5")]
    public void ResearchSitePortraitExplainsTheRaisedTechCeiling(short special, string title, string limit)
    {
        var lines = InformationEffectTooltips.SiteAt(Portrait, special);

        Assert.Equal(title, lines[0]);
        Assert.Contains(limit, string.Join(' ', lines));
    }

    [Fact]
    public void SpecialDoesNotChangeStatisticTooltips() =>
        Assert.StartsWith("RESISTANCE", InformationEffectTooltips.SiteAt(
            new Point(300, SiteInformationLayout.DataY(0)), SpecialSiteRules.Factory)[0]);
}
