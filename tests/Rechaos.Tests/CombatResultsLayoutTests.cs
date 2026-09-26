using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatResultsLayoutTests
{
    [Fact]
    public void PointerTargetsFollowRecoveredCombatResultsHandler()
    {
        Assert.Equal(new Rectangle(135, 157, 26, 23), CombatResultsLayout.Previous);
        Assert.Equal(new Rectangle(163, 157, 26, 23), CombatResultsLayout.Next);
        Assert.Equal(new Rectangle(137, 293, 49, 22), CombatResultsLayout.Ok);

        Assert.Equal(0, CombatResultsLayout.FriendlyForceSlotAt(new Point(205, 152)));
        Assert.Equal(3, CombatResultsLayout.FriendlyForceSlotAt(new Point(250, 250)));
        Assert.Null(CombatResultsLayout.FriendlyForceSlotAt(new Point(201, 152)));
    }

    [Fact]
    public void PageCounterArrowsAndPoliceStripFollowThePageRenderer()
    {
        // SCR-COMBAT-001, FND-COMBAT-007.
        Assert.Equal(new Rectangle(138, 137, 12, 7), CombatResultsLayout.PageNumber);
        Assert.Equal(new Rectangle(174, 137, 12, 7), CombatResultsLayout.PageCount);
        Assert.Equal(new Rectangle(170, 363, 26, 23), CombatResultsLayout.PreviousSource(firstPage: true));
        Assert.Equal(new Rectangle(118, 363, 26, 23), CombatResultsLayout.PreviousSource(firstPage: false));
        Assert.Equal(new Rectangle(196, 363, 26, 23), CombatResultsLayout.NextSource(lastPage: true));
        Assert.Equal(new Rectangle(144, 363, 26, 23), CombatResultsLayout.NextSource(lastPage: false));
        Assert.Equal(new Rectangle(135, 191, 54, 9), CombatResultsLayout.PoliceStrip);
        Assert.Equal(new Rectangle(0, 432, 54, 9), CombatResultsLayout.PoliceStripSource);
    }

    [Fact]
    public void OpponentPortraitsAreDimWithoutAResultAndTheChosenOneIsFramed()
    {
        // SCR-COMBAT-001, FND-COMBAT-007, FND-COMBAT-009.
        Assert.Equal(new Rectangle(96, 480, 32, 32), CombatResultsLayout.OpponentPortraitSource(3, true));
        Assert.Equal(new Rectangle(96, 594, 32, 32), CombatResultsLayout.OpponentPortraitSource(3, false));
        Assert.Equal(new Rectangle(305, 139, 34, 34), CombatResultsLayout.OpponentFrame(0));
        Assert.Equal(new Rectangle(305, 283, 34, 34), CombatResultsLayout.OpponentFrame(4));
        Assert.Equal(new Rectangle(120, 171, 34, 34), CombatResultsLayout.OpponentFrameSource);
    }

    [Fact]
    public void GridCellsCarryTwoTracksAndTheFocusOutline()
    {
        // SCR-COMBAT-001, FND-COMBAT-012.
        var cell = CombatResultsLayout.Force(3, enemy: true);
        Assert.Equal(new Rectangle(394, 205, 40, 40), cell);
        Assert.Equal(new Rectangle(394, 246, 40, 3), CombatResultsLayout.ForceTrack(cell, 0));
        Assert.Equal(new Rectangle(394, 250, 40, 3), CombatResultsLayout.ForceTrack(cell, 1));
        Assert.Equal(new Rectangle(392, 203, 44, 52), CombatResultsLayout.FocusOutline(cell));
        Assert.Equal(new Rectangle(468, 15, 44, 52), CombatResultsLayout.MutualFocusSource);
        Assert.Equal(40, CombatResultsLayout.ForceTrackFill(10));
        Assert.Equal(28, CombatResultsLayout.ForceTrackFill(7));
        // force_final is not clamped at 0 (RULE-COMBAT-002); the track draws nothing for it.
        Assert.Equal(0, CombatResultsLayout.ForceTrackFill(-3));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatResultsLayout.ForceTrack(cell, 2));
    }
}
