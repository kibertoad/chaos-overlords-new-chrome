using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class AttackCommandLayoutTests
{
    [Fact]
    public void PointerTargetsFollowRecoveredAttackHandlerGrid()
    {
        Assert.Equal(new Rectangle(202, 140, 32, 32), AttackCommandLayout.Opponent(0));
        Assert.Equal(new Rectangle(202, 284, 32, 32), AttackCommandLayout.Opponent(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => AttackCommandLayout.Opponent(5));

        Assert.Equal(new Rectangle(239, 140, 67, 89), AttackCommandLayout.TargetHit(0));
        Assert.Equal(new Rectangle(306, 140, 68, 89), AttackCommandLayout.TargetHit(1));
        Assert.Equal(new Rectangle(374, 140, 67, 89), AttackCommandLayout.TargetHit(2));
        Assert.Equal(new Rectangle(239, 229, 67, 88), AttackCommandLayout.TargetHit(3));
        Assert.Equal(new Rectangle(374, 229, 67, 88), AttackCommandLayout.TargetHit(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => AttackCommandLayout.TargetHit(6));
    }

    [Fact]
    public void PanelSitsAtTheCommandPanelOrigin()
    {
        // SCR-ATTACK-001, FND-ATTACK-003: screen (104,124)-(448,333).
        Assert.Equal(new Rectangle(104, 124, 344, 209), AttackCommandLayout.Panel);
    }

    [Fact]
    public void FacesAreCancelAboveConfirm()
    {
        // SCR-ATTACK-001, FND-ATTACK-003: Cancel tracks screen (137,261)-(187,284) and Confirm
        // screen (137,293)-(187,316).
        Assert.Equal(new Rectangle(137, 261, 50, 23), AttackCommandLayout.Cancel);
        Assert.Equal(new Rectangle(137, 293, 50, 23), AttackCommandLayout.Ok);
        // FND-UI-019: fn_00418E66's enabled and disabled Confirm faces.
        Assert.Equal(new Rectangle(50, 386, 50, 23), AttackCommandLayout.OkEnabledSource);
        Assert.Equal(new Rectangle(100, 386, 50, 23), AttackCommandLayout.OkDisabledSource);
    }

    [Fact]
    public void ActingGangSitsUnderTheCommandPanelPortraitCorner()
    {
        // SCR-ATTACK-001, FND-ATTACK-003: portrait local (26,17), items local (26,82), (48,82), (70,82).
        Assert.Equal(new Rectangle(130, 141, 64, 64), AttackCommandLayout.ActorPortrait);
        Assert.Equal(new Rectangle(130, 206, 20, 20), AttackCommandLayout.ActorItem(0));
        Assert.Equal(new Rectangle(152, 206, 20, 20), AttackCommandLayout.ActorItem(1));
        Assert.Equal(new Rectangle(174, 206, 20, 20), AttackCommandLayout.ActorItem(2));
    }

    [Fact]
    public void TargetPortraitsAndItemsFollowTheDoubleClickRectangles()
    {
        // SCR-ATTACK-001, FND-ATTACK-004: portrait (136 + 68 * (k % 3), 17 + 90 * (k / 3)), and
        // items 65 rows below its top at x offsets 0, 22 and 44.
        Assert.Equal(new Rectangle(240, 141, 64, 64), AttackCommandLayout.TargetPortrait(0));
        Assert.Equal(new Rectangle(308, 141, 64, 64), AttackCommandLayout.TargetPortrait(1));
        Assert.Equal(new Rectangle(376, 141, 64, 64), AttackCommandLayout.TargetPortrait(2));
        Assert.Equal(new Rectangle(240, 231, 64, 64), AttackCommandLayout.TargetPortrait(3));
        Assert.Equal(new Rectangle(376, 231, 64, 64), AttackCommandLayout.TargetPortrait(5));
        Assert.Equal(new Rectangle(240, 206, 20, 20), AttackCommandLayout.TargetItem(0, 0));
        Assert.Equal(new Rectangle(398, 296, 20, 20), AttackCommandLayout.TargetItem(5, 1));
        Assert.Equal(new Rectangle(420, 296, 20, 20), AttackCommandLayout.TargetItem(5, 2));
    }

    [Fact]
    public void MarksFollowTheRecordedCrops()
    {
        // SCR-ATTACK-001, FND-ATTACK-002.
        Assert.Equal(new Rectangle(201, 139, 34, 34), AttackCommandLayout.OpponentFrame(0));
        Assert.Equal(new Rectangle(201, 283, 34, 34), AttackCommandLayout.OpponentFrame(4));
        Assert.Equal(new Rectangle(120, 171, 34, 34), AttackCommandLayout.OpponentFrameSource);
        Assert.Equal(new Rectangle(247, 142, 48, 48), AttackCommandLayout.TargetMarker(0));
        Assert.Equal(new Rectangle(383, 232, 48, 48), AttackCommandLayout.TargetMarker(5));
        Assert.Equal(new Rectangle(66, 299, 48, 48), AttackCommandLayout.TargetMarkerSource);
    }

    [Fact]
    public void OpponentArtComesFromTheEnabledOrDisabledRow()
    {
        // SCR-ATTACK-001, FND-ATTACK-001: surface 6 rows 480 and 594, column by portrait number.
        Assert.Equal(new Rectangle(96, 480, 32, 32), AttackCommandLayout.OpponentSource(3, enabled: true));
        Assert.Equal(new Rectangle(96, 594, 32, 32), AttackCommandLayout.OpponentSource(3, enabled: false));
    }

    [Fact]
    public void TargetCellFollowsTheHandlersFormula()
    {
        // FND-ATTACK-003: cell 3 * (y > 104) + (x > 201) + (x > 269) in local (135,16)-(337,193).
        Assert.Equal(new Rectangle(239, 140, 202, 177), AttackCommandLayout.TargetArea);
        Assert.Equal(-1, AttackCommandLayout.TargetCellAt(new Point(238, 140)));
        Assert.Equal(0, AttackCommandLayout.TargetCellAt(new Point(239, 140)));
        Assert.Equal(0, AttackCommandLayout.TargetCellAt(new Point(305, 228)));
        Assert.Equal(1, AttackCommandLayout.TargetCellAt(new Point(306, 140)));
        Assert.Equal(2, AttackCommandLayout.TargetCellAt(new Point(374, 140)));
        Assert.Equal(3, AttackCommandLayout.TargetCellAt(new Point(239, 229)));
        Assert.Equal(5, AttackCommandLayout.TargetCellAt(new Point(440, 316)));
        Assert.Equal(-1, AttackCommandLayout.TargetCellAt(new Point(441, 316)));
        Assert.Equal(-1, AttackCommandLayout.TargetCellAt(new Point(440, 317)));
        for (var cell = 0; cell < AttackCommandLayout.VisibleTargets; cell++)
        {
            var hit = AttackCommandLayout.TargetHit(cell);
            Assert.Equal(cell, AttackCommandLayout.TargetCellAt(hit.Location));
            Assert.Equal(cell, AttackCommandLayout.TargetCellAt(new Point(hit.Right - 1, hit.Bottom - 1)));
        }
    }

    [Fact]
    public void EnterAndExecuteConfirm()
    {
        // SCR-ATTACK-001, FND-ATTACK-003: 0x0D and 0x2B confirm.
        Assert.Equal([Keys.Enter, Keys.Execute], AttackCommandLayout.ConfirmKeys);
    }
}
