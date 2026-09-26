using Rechaos.Game;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangMultiSelectionTests
{
    private const int Sector = 9;

    /// <summary>Every panel standing directly on the Sector workspace.</summary>
    private static PanelReturnScreens OverTheWorkspace => new()
    {
        Commands = ClientScreen.Sector,
        GangDetails = ClientScreen.Sector,
        SiteDetails = ClientScreen.Sector,
        SectorGangs = ClientScreen.Sector
    };

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
    public void PanelsOpenedOverTheWorkspaceKeepTheSelection()
    {
        var returns = OverTheWorkspace;

        Assert.True(GangSelectionScreens.Keeps(ClientScreen.Sector, returns));
        Assert.True(GangSelectionScreens.Keeps(ClientScreen.Commands, returns));
        Assert.True(GangSelectionScreens.Keeps(ClientScreen.ItemInformation, returns));
        Assert.True(GangSelectionScreens.Keeps(ClientScreen.Gang, returns));
        Assert.True(GangSelectionScreens.Keeps(ClientScreen.Site, returns));
        Assert.True(GangSelectionScreens.Keeps(ClientScreen.SectorGangs, returns));
    }

    [Fact]
    public void SiteDetailsOpenedOverTheCommandOverlayKeepTheSelection()
    {
        // The bulk influence picker opens a site on a double-click, leaving the workspace two
        // panels down; inspecting a site must not lose the picks the order is being given for.
        var returns = OverTheWorkspace with { SiteDetails = ClientScreen.Commands };

        Assert.True(GangSelectionScreens.Keeps(ClientScreen.Site, returns));
    }

    [Fact]
    public void GangDetailsOpenedOverTheCommandOverlayKeepTheSelection()
    {
        // The equipment picker opens the ordered gang's information on a portrait double-click.
        var returns = OverTheWorkspace with { GangDetails = ClientScreen.Commands };

        Assert.True(GangSelectionScreens.Keeps(ClientScreen.Gang, returns));
    }

    [Fact]
    public void SiteDetailsOpenedOverAnotherScreenForgetTheSelection()
    {
        Assert.False(GangSelectionScreens.Keeps(
            ClientScreen.Site, OverTheWorkspace with { SiteDetails = ClientScreen.Search }));
        // The overlay itself came from the city map, so nothing in the stack is the workspace.
        Assert.False(GangSelectionScreens.Keeps(
            ClientScreen.Site,
            OverTheWorkspace with
            {
                SiteDetails = ClientScreen.Commands,
                Commands = ClientScreen.City
            }));
    }

    [Fact]
    public void LeavingTheWorkspaceForgetsTheSelection()
    {
        var returns = OverTheWorkspace;

        Assert.False(GangSelectionScreens.Keeps(ClientScreen.City, returns));
        Assert.False(GangSelectionScreens.Keeps(ClientScreen.Handoff, returns));
        Assert.False(GangSelectionScreens.Keeps(ClientScreen.Hire, returns));
        Assert.False(GangSelectionScreens.Keeps(ClientScreen.Endgame, returns));
    }

    [Fact]
    public void PanelsOpenedOverTheCityMapForgetTheSelection()
    {
        var returns = new PanelReturnScreens
        {
            Commands = ClientScreen.City,
            GangDetails = ClientScreen.City,
            SiteDetails = ClientScreen.City,
            SectorGangs = ClientScreen.City
        };

        Assert.False(GangSelectionScreens.Keeps(ClientScreen.Commands, returns));
        Assert.False(GangSelectionScreens.Keeps(ClientScreen.ItemInformation, returns));
        Assert.False(GangSelectionScreens.Keeps(ClientScreen.Gang, returns));
        Assert.False(GangSelectionScreens.Keeps(ClientScreen.Site, returns));
        Assert.False(GangSelectionScreens.Keeps(ClientScreen.SectorGangs, returns));
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
