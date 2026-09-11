using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SectorGangsUiTests
{
    [Fact]
    public void LayoutMatchesOriginalGangsInSectorPanel()
    {
        Assert.Equal(new Rectangle(104, 125, 344, 209), SectorGangsLayout.Panel);
        Assert.Equal(new Rectangle(134, 135, 55, 65), SectorGangsLayout.Portrait);
        Assert.Equal(173, SectorGangsLayout.ValueY(0));
        Assert.Equal(311, SectorGangsLayout.ValueY(15));
        Assert.Equal(EquipmentCommandLayout.Ok, SectorGangsLayout.Ok);
    }

    [Fact]
    public void LayoutRejectsRowsOutsideSixteenOriginalFields()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.ValueY(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.ValueY(16));
    }
}
