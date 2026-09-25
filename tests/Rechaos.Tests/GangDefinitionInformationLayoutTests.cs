using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangDefinitionInformationLayoutTests
{
    [Fact]
    public void UsesTheRecoveredAlternatePanelGeometry()
    {
        Assert.Equal(new Rectangle(128, 124, 320, 209), GangDefinitionInformationLayout.Panel);
        Assert.Equal(new Rectangle(0, 0, 320, 209), GangDefinitionInformationLayout.BackgroundSource);
        Assert.Equal(new Rectangle(154, 141, 64, 64), GangDefinitionInformationLayout.Portrait);
        Assert.Equal(new Rectangle(161, 293, 49, 22), GangDefinitionInformationLayout.Ok);
        Assert.Equal(228, GangDefinitionInformationLayout.NameLeft);
        Assert.Equal(228, GangDefinitionInformationLayout.DescriptionLeft);
        Assert.Equal(180, GangDefinitionInformationLayout.DescriptionClearWidth);
        Assert.Equal([169, 178, 187], Enumerable.Range(0, 3)
            .Select(GangDefinitionInformationLayout.DescriptionY));
        Assert.Equal(300, GangDefinitionInformationLayout.LeftValueLeft);
        Assert.Equal(396, GangDefinitionInformationLayout.RightValueLeft);
        Assert.Equal(new Rectangle(300, 216, 12, 7),
            GangInformationLayout.ValueField(GangDefinitionInformationLayout.LeftValueLeft, 216));
        Assert.Equal(new Rectangle(396, 216, 12, 7),
            GangInformationLayout.ValueField(GangDefinitionInformationLayout.RightValueLeft, 216));
        Assert.Equal([243, 252, 270, 279, 288, 297, 306], Enumerable.Range(0, 7)
            .Select(GangDefinitionInformationLayout.StatisticY));
        Assert.Equal(216, GangDefinitionInformationLayout.ForceY);
        Assert.Equal(225, GangDefinitionInformationLayout.TechLevelY);
    }

    // SCR-GANG-001, FND-GANG-010: base values 18 pixels left, at x 282 and 378, under the pattern.
    [Fact]
    public void BaseValuesSitUnderTheFourDimmedAreas()
    {
        Assert.Equal(282, GangDefinitionInformationLayout.LeftValueLeft
            - GangInformationLayout.BaseValueOffset);
        Assert.Equal(378, GangDefinitionInformationLayout.RightValueLeft
            - GangInformationLayout.BaseValueOffset);
        Assert.Equal(
        [
            new Rectangle(282, 243, 12, 18), new Rectangle(378, 243, 12, 18),
            new Rectangle(282, 270, 12, 45), new Rectangle(378, 270, 12, 45)
        ], GangDefinitionInformationLayout.BaseValueDimAreas);
    }
}
