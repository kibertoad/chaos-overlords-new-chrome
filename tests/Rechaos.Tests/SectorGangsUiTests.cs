using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SectorGangsUiTests
{
    [Fact]
    public void LayoutMatchesOriginalGangsInSectorPanel()
    {
        Assert.Equal(new Rectangle(104, 124, 344, 209), SectorGangsLayout.Panel);
        Assert.Equal(new Rectangle(144, 158, 32, 32), SectorGangsLayout.GangCard(0));
        Assert.Equal(new Rectangle(304, 158, 32, 32), SectorGangsLayout.GangCard(5));
        Assert.Equal(154, SectorGangsLayout.ValueRight(0));
        Assert.Equal(314, SectorGangsLayout.ValueRight(5));
        Assert.Equal(192, SectorGangsLayout.ValueY(0));
        Assert.Equal(330, SectorGangsLayout.ValueY(15));
        Assert.Equal(EquipmentCommandLayout.Ok, SectorGangsLayout.Ok);
    }

    [Fact]
    public void LayoutRejectsRowsOutsideSixteenOriginalFields()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.ValueY(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.ValueY(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.GangCard(6));
    }
}
