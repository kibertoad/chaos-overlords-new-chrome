using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SectorGangsUiTests
{
    // SCR-UI-005, FND-UI-014.
    [Fact]
    public void LayoutMatchesOriginalGangsInSectorPanel()
    {
        Assert.Equal(new Rectangle(104, 124, 344, 209), SectorGangsLayout.Panel);
        Assert.Equal(new Rectangle(135, 135, 54, 52), SectorGangsLayout.SectorTile);
        Assert.Equal(new Point(156, 190), SectorGangsLayout.SectorCode);
        Assert.Equal(new Rectangle(248, 138, 32, 32), SectorGangsLayout.GangCard(0));
        Assert.Equal(new Rectangle(408, 138, 32, 32), SectorGangsLayout.GangCard(5));
        Assert.Equal(258, SectorGangsLayout.ValueLeft(0));
        Assert.Equal(418, SectorGangsLayout.ValueLeft(5));
        Assert.Equal([172, 181, 191, 200, 209, 218, 228, 237, 246, 255, 264, 274, 283, 292, 301, 310],
            Enumerable.Range(0, 16).Select(SectorGangsLayout.ValueY));
        Assert.Equal(new Rectangle(137, 293, 49, 22), SectorGangsLayout.Ok);
    }

    // SCR-UI-005, FND-UI-014: a column letter A to H and a row digit 1 to 8.
    [Fact]
    public void SectorCodeNamesTheColumnAndRow()
    {
        Assert.Equal("A1", SectorGangsLayout.SectorCodeText(0));
        Assert.Equal("H1", SectorGangsLayout.SectorCodeText(7));
        Assert.Equal("C2", SectorGangsLayout.SectorCodeText(10));
        Assert.Equal("H8", SectorGangsLayout.SectorCodeText(63));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.SectorCodeText(64));
    }

    [Fact]
    public void LayoutRejectsRowsOutsideSixteenOriginalFields()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.ValueY(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.ValueY(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangsLayout.GangCard(6));
    }
}
