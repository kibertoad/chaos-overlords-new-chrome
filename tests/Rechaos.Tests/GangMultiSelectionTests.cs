using Rechaos.Game;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangMultiSelectionTests
{
    private const int Sector = 9;

    [Fact]
    public void CtrlPickingTheSameGangTwiceTakesItBackOut()
    {
        var selection = new GangMultiSelection();

        selection.Toggle(new GangId(1), Sector);
        selection.Toggle(new GangId(2), Sector);
        selection.Toggle(new GangId(1), Sector);

        Assert.Equal([new GangId(2)], selection.Gangs);
        Assert.False(selection.Contains(new GangId(1)));
        Assert.False(selection.IsBulk);
    }

    [Fact]
    public void PicksKeepTheOrderTheyWereMadeIn()
    {
        var selection = new GangMultiSelection();

        selection.Toggle(new GangId(7), Sector);
        selection.Toggle(new GangId(3), Sector);
        selection.Toggle(new GangId(5), Sector);

        Assert.Equal([new GangId(7), new GangId(3), new GangId(5)], selection.Gangs);
        Assert.True(selection.IsBulk);
    }

    [Fact]
    public void PickingInAnotherSectorStartsTheSelectionOver()
    {
        var selection = new GangMultiSelection();
        selection.Toggle(new GangId(1), Sector);
        selection.Toggle(new GangId(2), Sector);

        selection.Toggle(new GangId(3), Sector + 1);

        Assert.Equal([new GangId(3)], selection.Gangs);
    }

    [Fact]
    public void ShowingAnotherSectorForgetsTheSelectionMadeInThisOne()
    {
        var selection = new GangMultiSelection();
        selection.Toggle(new GangId(1), Sector);

        selection.KeepOnly(Sector);
        Assert.Equal(1, selection.Count);

        selection.KeepOnly(Sector + 1);
        Assert.Equal(0, selection.Count);
    }

    [Fact]
    public void AnEmptiedSelectionBelongsToNoSector()
    {
        var selection = new GangMultiSelection();
        selection.Toggle(new GangId(1), Sector);
        selection.Toggle(new GangId(1), Sector);

        selection.KeepOnly(Sector);

        Assert.Equal(0, selection.Count);
        Assert.Empty(selection.Gangs);
    }

    [Fact]
    public void TheStatusLineCountsTheGangsAndFitsTheConsole()
    {
        Assert.Equal("1 GANG SELECTED", GangMultiSelection.Status(1));
        Assert.Equal("4 GANGS SELECTED", GangMultiSelection.Status(4));
        for (var count = 0; count <= MatchLimits.FriendlyGangsPerSector; count++)
            Assert.True(CityStatusMessage.Fits(GangMultiSelection.Status(count)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GangMultiSelection.Status(-1));
    }

    [Fact]
    public void APickOutsideTheCityIsRefused()
    {
        var selection = new GangMultiSelection();

        Assert.Throws<ArgumentOutOfRangeException>(() => selection.Toggle(new GangId(1), -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => selection.Toggle(new GangId(1), MatchLimits.SectorCount));
    }
}
