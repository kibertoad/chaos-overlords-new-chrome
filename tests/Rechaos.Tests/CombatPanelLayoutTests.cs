using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatPanelLayoutTests
{
    [Fact]
    public void EquipmentAperturesMatchTheNativeDetailedCombatPanel()
    {
        Assert.Equal(new Rectangle(204, 172, 48, 48), CombatPanelLayout.EquipmentItem(false, 0));
        Assert.Equal(new Rectangle(204, 221, 48, 48), CombatPanelLayout.EquipmentItem(false, 1));
        Assert.Equal(new Rectangle(204, 270, 48, 48), CombatPanelLayout.EquipmentItem(false, 2));
        Assert.Equal(new Rectangle(393, 172, 48, 48), CombatPanelLayout.EquipmentItem(true, 0));
        Assert.Equal(new Rectangle(393, 221, 48, 48), CombatPanelLayout.EquipmentItem(true, 1));
        Assert.Equal(new Rectangle(393, 270, 48, 48), CombatPanelLayout.EquipmentItem(true, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatPanelLayout.EquipmentItem(false, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatPanelLayout.EquipmentItem(false, 3));
    }

    [Fact]
    public void TracksAndExitFollowTheDetailedCombatReading()
    {
        // SCR-COMBAT-002: tracks from buffer x 152 and 225, rows 260 and 267 (FND-COMBAT-010).
        Assert.Equal(new Rectangle(256, 240, 60, 3), CombatPanelLayout.ForceBar(false, 0));
        Assert.Equal(new Rectangle(256, 247, 60, 3), CombatPanelLayout.ForceBar(false, 1));
        Assert.Equal(new Rectangle(329, 240, 60, 3), CombatPanelLayout.ForceBar(true, 0));
        Assert.Equal(new Rectangle(329, 247, 60, 3), CombatPanelLayout.ForceBar(true, 1));
        // The red track, then 6 pixels of the green strip per point (FND-COMBAT-009).
        Assert.Equal(new Rectangle(354, 3, 60, 3), CombatPanelLayout.RedTrackSource);
        Assert.Equal(new Rectangle(354, 0, 42, 3), CombatPanelLayout.GreenTrackSource(42));
        Assert.Equal(42, CombatPanelLayout.TrackFill(7));
        Assert.Equal(60, CombatPanelLayout.TrackFill(10));
        Assert.Equal(0, CombatPanelLayout.TrackFill(-1));
        // The Exit face, local (33,169)-(82,191), and its pressed look.
        Assert.Equal(new Rectangle(137, 293, 50, 23), CombatPanelLayout.Exit);
        Assert.Equal(new Rectangle(50, 386, 50, 23), CombatPanelLayout.ExitPressedSource);
    }

    [Fact]
    public void ReleasingOnTheExitFaceEndsThePresentation()
    {
        // SCR-COMBAT-002: the face is tracked and acts on release over it (FND-COMBAT-010).
        var exit = new DetailedCombatExit();
        var face = new Point(150, 300);

        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(face, down: true, wasDown: false));
        Assert.True(exit.ShowsPressed);
        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(new Point(300, 200), true, true));
        Assert.False(exit.ShowsPressed);
        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(face, true, true));
        Assert.True(exit.ShowsPressed);
        Assert.Equal(DetailedCombatPointerResult.EndPresentation, exit.Update(face, false, true));
        Assert.False(exit.Tracking);
    }

    [Fact]
    public void ReleasingOffTheExitFaceKeepsThePresentation()
    {
        var exit = new DetailedCombatExit();

        exit.Update(new Point(137, 293), down: true, wasDown: false);
        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(new Point(187, 316), false, true));
        Assert.False(exit.Tracking);
        Assert.False(exit.ShowsPressed);
    }

    [Fact]
    public void APressOutsideThePanelIsRefusedAndOneInsideDoesNothing()
    {
        // SCR-COMBAT-002: outside screen (104,124)-(448,333) plays the rejected sound.
        var exit = new DetailedCombatExit();

        Assert.Equal(DetailedCombatPointerResult.Rejected, exit.Update(new Point(50, 50), true, false));
        Assert.Equal(DetailedCombatPointerResult.Rejected, exit.Update(new Point(448, 200), true, false));
        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(new Point(447, 332), true, false));
        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(new Point(200, 200), false, true));
        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(null, true, false));
        Assert.False(exit.Tracking);
    }
}
