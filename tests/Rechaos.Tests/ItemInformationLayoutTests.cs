using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ItemInformationLayoutTests
{
    [Fact]
    public void UsesTheRecoveredAlternatePanelGeometry()
    {
        Assert.Equal(new Rectangle(128, 124, 320, 209), ItemInformationLayout.Panel);
        Assert.Equal(new Rectangle(0, 0, 320, 209), ItemInformationLayout.BackgroundSource);
        Assert.Equal(new Rectangle(162, 141, 48, 48), ItemInformationLayout.Portrait);
        Assert.Equal(new Rectangle(161, 293, 49, 22), ItemInformationLayout.Ok);
        Assert.Equal(228, ItemInformationLayout.NameLeft);
        Assert.Equal(408, ItemInformationLayout.TypeRight);
        Assert.Equal(30, ItemInformationLayout.DescriptionColumns);
        Assert.Equal([169, 178, 187], Enumerable.Range(0, 3)
            .Select(row => ItemInformationLayout.DescriptionY + row * 9));
        Assert.Equal(new Rectangle(300, 216, 12, 7),
            GangInformationLayout.ValueField(ItemInformationLayout.LeftValueLeft, 216));
        Assert.Equal(new Rectangle(396, 216, 12, 7),
            GangInformationLayout.ValueField(ItemInformationLayout.RightValueLeft, 216));
    }

    [Fact]
    public void PreservesAuthoredFixedWidthDescriptionRows()
    {
        var description = "TITANIUM ALLOY.               "
            + "GOOD FOR BUSTING IN A FEW     "
            + "HARD HEADS.";

        Assert.Equal(
        [
            "TITANIUM ALLOY.",
            "GOOD FOR BUSTING IN A FEW",
            "HARD HEADS."
        ], ItemInformationLayout.DescriptionLines(description));
    }
}
