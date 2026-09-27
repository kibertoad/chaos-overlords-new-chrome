using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The Cancel and confirm faces of SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001 and SCR-MOVE-001.
/// </summary>
public sealed class CommandPanelFacesTests
{
    [Fact]
    public void TargetsAreTheHalfOpenFortyNineByTwentyTwoRectangles()
    {
        Assert.Equal(new Rectangle(137, 261, 49, 22), CommandPanelFaces.CancelHit);
        Assert.Equal(new Rectangle(137, 293, 49, 22), CommandPanelFaces.ConfirmHit);
        Assert.Equal(new Rectangle(137, 261, 49, 22), EquipmentCommandLayout.Cancel);
        Assert.Equal(new Rectangle(137, 293, 49, 22), EquipmentCommandLayout.Ok);
        Assert.Equal(CommandPanelButton.Cancel, CommandPanelFaces.ButtonAt(new Point(137, 261)));
        Assert.Null(CommandPanelFaces.ButtonAt(new Point(186, 261)));
        Assert.Null(CommandPanelFaces.ButtonAt(new Point(137, 283)));
        Assert.Equal(CommandPanelButton.Confirm, CommandPanelFaces.ButtonAt(new Point(185, 314)));
        Assert.Null(CommandPanelFaces.ButtonAt(new Point(136, 293)));
    }

    [Fact]
    public void FacesAreFiftyByTwentyThreeImagesOfPx00129()
    {
        // FND-UI-019, FND-COMLINK-003.
        Assert.Equal(new Rectangle(137, 261, 50, 23), CommandPanelFaces.Face(CommandPanelButton.Cancel));
        Assert.Equal(new Rectangle(137, 293, 50, 23), CommandPanelFaces.Face(CommandPanelButton.Confirm));
        Assert.Null(CommandPanelFaces.Source(CommandPanelFaceState.NotDrawn));
        Assert.Equal(new Rectangle(50, 386, 50, 23), CommandPanelFaces.Source(CommandPanelFaceState.Enabled));
        Assert.Equal(new Rectangle(100, 386, 50, 23), CommandPanelFaces.Source(CommandPanelFaceState.Disabled));
        Assert.Equal(new Rectangle(50, 409, 50, 23), CommandPanelFaces.HeldSource(CommandPanelButton.Cancel));
        Assert.Equal(new Rectangle(50, 386, 50, 23), CommandPanelFaces.HeldSource(CommandPanelButton.Confirm));
    }

    [Fact]
    public void ConfirmFaceIsDrawnOnlyForAnExistingOrderUntilTheSelectionChanges()
    {
        // FND-EQUIP-010, FND-GIVE-001, FND-SELL-001, FND-MOVE-004.
        Assert.Equal(CommandPanelFaceState.NotDrawn, CommandPanelFaces.OnOpening(false));
        Assert.Equal(CommandPanelFaceState.Enabled, CommandPanelFaces.OnOpening(true));
        Assert.Equal(CommandPanelFaceState.Enabled, CommandPanelFaces.AfterChange(true));
        Assert.Equal(CommandPanelFaceState.Disabled, CommandPanelFaces.AfterChange(false));
    }
}
