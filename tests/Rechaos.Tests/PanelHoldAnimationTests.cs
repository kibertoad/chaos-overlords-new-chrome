using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

// FND-UI-047, RULE-UI-008: a panel that animates on timer slot 0 in its own loop reads the flag at
// the end of each pass, after the event switch. A face held through the held-button helper or a
// page arrow of Last Turn Events keeps that loop inside the hold, and the hold leaves the flag
// alone, so the animation stops and the pass the release ends takes one tick; the others are lost.
public sealed class PanelHoldAnimationTests
{
    private static TimeSpan Tick(long count) => TimeSpan.FromTicks(PresentationClock.Period.Ticks * count);

    [Fact]
    public void AnItemRotationStopsWhileAFaceIsHeldAndTakesOneTickAfterIt()
    {
        // SCR-UI-006, SCR-SELL-001, SCR-GIVE-001.
        var pump = new EventPumpClock();
        pump.Update(Tick(10), holding: false);
        var held = ItemRotationPresentation.Frame(pump.Time);
        Assert.Equal(ItemRotationPresentation.Frame(10), held);
        for (var tick = 11; tick <= 40; tick++)
        {
            pump.Update(Tick(tick), holding: true);
            Assert.Equal(held, ItemRotationPresentation.Frame(pump.Time));
        }

        pump.Update(Tick(40) + TimeSpan.FromMilliseconds(1), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(11), ItemRotationPresentation.Frame(pump.Time));
        pump.Update(Tick(41), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(12), ItemRotationPresentation.Frame(pump.Time));
    }

    [Fact]
    public void APageTurnedByAHeldArrowShowsTheSecondFrameAtOnce()
    {
        // SCR-EVENT-001: the page change sets the counter to 0 and the same pass takes the tick
        // the flag kept through the hold.
        var pump = new EventPumpClock();
        pump.Update(Tick(10), holding: false);
        for (var tick = 11; tick <= 20; tick++) pump.Update(Tick(tick), holding: true);
        // The release is handled on a pass that still counts as held, and turns the page.
        var shown = pump.Ticks;
        Assert.Equal(ItemRotationPresentation.Frame(0), ItemRotationPresentation.FrameAfter(pump.Ticks - shown));

        pump.Update(Tick(20) + TimeSpan.FromMilliseconds(1), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(1), ItemRotationPresentation.FrameAfter(pump.Ticks - shown));
        pump.Update(Tick(21), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(2), ItemRotationPresentation.FrameAfter(pump.Ticks - shown));
    }

    [Fact]
    public void APageTurnedWithoutAHoldStartsAtTheFirstFrame()
    {
        var pump = new EventPumpClock();
        pump.Update(Tick(10) + TimeSpan.FromMilliseconds(30), holding: false);
        var shown = pump.Ticks;
        pump.Update(Tick(10) + TimeSpan.FromMilliseconds(150), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(0), ItemRotationPresentation.FrameAfter(pump.Ticks - shown));
        pump.Update(Tick(11), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(1), ItemRotationPresentation.FrameAfter(pump.Ticks - shown));
        pump.Update(Tick(25), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(0), ItemRotationPresentation.FrameAfter(pump.Ticks - shown));
    }

    [Fact]
    public void ACounterStartedByAHeldFacesReleaseLeavesTheKeptTickToThatPanel()
    {
        // SCR-GANG-002: the Item Information panel's last pass takes the kept tick, so the gang
        // information panel it returns to starts again at frame 0.
        var pump = new EventPumpClock();
        pump.Update(Tick(10), holding: false);
        Assert.Equal(pump.Ticks, pump.TicksAfterHold(Tick(10)));
        pump.Update(Tick(10) + TimeSpan.FromMilliseconds(100), holding: true);
        Assert.Equal(pump.Ticks, pump.TicksAfterHold(Tick(10) + TimeSpan.FromMilliseconds(100)));
        for (var tick = 11; tick <= 20; tick++) pump.Update(Tick(tick), holding: true);
        var start = pump.TicksAfterHold(Tick(20));

        pump.Update(Tick(20) + TimeSpan.FromMilliseconds(1), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(0), ItemRotationPresentation.FrameAfter(pump.Ticks - start));
        pump.Update(Tick(21), holding: false);
        Assert.Equal(ItemRotationPresentation.Frame(1), ItemRotationPresentation.FrameAfter(pump.Ticks - start));
    }

    [Fact]
    public void TheComlinkCaretStopsWhileCancelOrSendIsHeld()
    {
        // SCR-COMLINK-002: every third tick the Send loop consumes switches the caret.
        var pump = new EventPumpClock();
        var caret = new ComlinkCaretCadence();
        pump.Update(TimeSpan.Zero, holding: false);
        caret.Reset(pump.Time);
        for (var tick = 1; tick <= 2; tick++)
        {
            pump.Update(Tick(tick), holding: false);
            caret.Advance(pump.Time);
        }
        Assert.False(caret.UsesInverseGlyph);
        for (var tick = 3; tick <= 30; tick++)
        {
            pump.Update(Tick(tick), holding: true);
            caret.Advance(pump.Time);
            Assert.False(caret.UsesInverseGlyph);
        }

        // The release takes the third tick and switches the caret; three more switch it back.
        pump.Update(Tick(30) + TimeSpan.FromMilliseconds(1), holding: false);
        caret.Advance(pump.Time);
        Assert.True(caret.UsesInverseGlyph);
        for (var tick = 31; tick <= 33; tick++)
        {
            pump.Update(Tick(tick), holding: false);
            caret.Advance(pump.Time);
            Assert.Equal(tick != 33, caret.UsesInverseGlyph);
        }
    }

    [Fact]
    public void TheIdleWarningLineStopsWhileAFaceIsHeld()
    {
        // SCR-OPTIONS-001: the line shows for six ticks and is dark for two.
        var pump = new EventPumpClock();
        pump.Update(Tick(5), holding: false);
        Assert.True(IdleGangWarningLayout.LineShown(pump.Time));
        for (var tick = 6; tick <= 16; tick++)
        {
            pump.Update(Tick(tick), holding: true);
            Assert.True(IdleGangWarningLayout.LineShown(pump.Time));
        }
        pump.Update(Tick(16) + TimeSpan.FromMilliseconds(1), holding: false);
        Assert.False(IdleGangWarningLayout.LineShown(pump.Time));
    }

    [Fact]
    public void ADetailedCombatClipStopsWhileExitIsHeldAndTakesOneTickAfterIt()
    {
        // SCR-COMBAT-002: a release outside the Exit face lets the clip go on.
        var period = TimeSpan.FromMilliseconds(CombatAnimationRouting.FrameMilliseconds);
        var player = new CombatAnimationPlayer();
        player.Enqueue(new CombatAnimationClip(1, new GangId(10), new GangId(20), 3, 2, false, default));
        player.Advance(period);
        Assert.Equal(1, player.TimelineTick);

        for (var pass = 0; pass < 40; pass++)
        {
            Assert.Empty(player.Advance(period / 2, holding: true));
            Assert.Equal(1, player.TimelineTick);
        }

        // The first pass after the release takes one of the twenty ticks that fell.
        player.Advance(TimeSpan.Zero, holding: false);
        Assert.Equal(2, player.TimelineTick);
        player.Advance(period);
        Assert.Equal(3, player.TimelineTick);
    }

    [Fact]
    public void ACombatHoldShorterThanATickLosesNothing()
    {
        var period = TimeSpan.FromMilliseconds(CombatAnimationRouting.FrameMilliseconds);
        var player = new CombatAnimationPlayer();
        player.Enqueue(new CombatAnimationClip(1, new GangId(10), new GangId(20), 3, 2, false, default));
        player.Advance(period / 2);
        player.Advance(period / 4, holding: true);
        player.Advance(TimeSpan.Zero, holding: false);
        Assert.Equal(0, player.TimelineTick);
        player.Advance(period / 4 + TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, player.TimelineTick);
    }

    [Fact]
    public void HoldingDetailedCombatsExitFaceTracksTheHold()
    {
        var exit = new DetailedCombatExit();
        var face = CombatPanelLayout.Exit.Center;
        exit.Update(face, down: true, wasDown: false);
        Assert.True(exit.Tracking);
        exit.Update(new Point(0, 0), down: true, wasDown: true);
        Assert.True(exit.Tracking);
        Assert.Equal(DetailedCombatPointerResult.None, exit.Update(new Point(0, 0), down: false, wasDown: true));
        Assert.False(exit.Tracking);
    }
}
