using System.Reflection;
using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

// RULE-TIMER-003, RULE-UI-008, FND-UI-044, FND-UI-046: while an offer, a console tile, a gang
// card's portrait or a held-button face is held, the original dispatches window messages without
// calling the event pump, so the steps the pump drives stop. Timer slot 0's flag keeps one tick of
// the hold for the pump's first call after the release; the others are lost. A soundtrack fade
// stops the pump in the same way (FND-AUDIO-017).
public sealed class EventPumpClockTests
{
    private static TimeSpan Tick(long count) => TimeSpan.FromTicks(PresentationClock.Period.Ticks * count);

    [Fact]
    public void OutsideAHoldThePumpTakesEveryTick()
    {
        var pump = new EventPumpClock();
        pump.Update(Tick(3) + TimeSpan.FromMilliseconds(5), holding: false);
        Assert.Equal(3, pump.Ticks);
        Assert.Equal(Tick(3) + TimeSpan.FromMilliseconds(5), pump.Time);
        pump.Update(Tick(40), holding: false);
        Assert.Equal(40, pump.Ticks);
    }

    [Fact]
    public void AHoldStopsThePumpAndItsReleaseKeepsOneTick()
    {
        var pump = new EventPumpClock();
        pump.Update(Tick(10), holding: false);
        var before = pump.Time;
        for (var tick = 11; tick <= 30; tick++)
        {
            pump.Update(Tick(tick), holding: true);
            Assert.Equal(10, pump.Ticks);
            Assert.Equal(before, pump.Time);
        }

        // The first pass after the release takes the one tick the flag kept.
        pump.Update(Tick(30) + TimeSpan.FromMilliseconds(1), holding: false);
        Assert.Equal(11, pump.Ticks);
        // From then on the pump runs nineteen ticks behind the clock.
        pump.Update(Tick(31), holding: false);
        Assert.Equal(12, pump.Ticks);
        pump.Update(Tick(100), holding: false);
        Assert.Equal(81, pump.Ticks);
    }

    [Fact]
    public void AHoldWithinOneTickLosesNothing()
    {
        var pump = new EventPumpClock();
        pump.Update(Tick(5) + TimeSpan.FromMilliseconds(10), holding: false);
        pump.Update(Tick(5) + TimeSpan.FromMilliseconds(100), holding: true);
        pump.Update(Tick(5) + TimeSpan.FromMilliseconds(150), holding: false);
        Assert.Equal(5, pump.Ticks);

        // One tick falls during the hold: the flag keeps it and nothing is lost.
        pump.Update(Tick(6) - TimeSpan.FromMilliseconds(1), holding: true);
        pump.Update(Tick(6) + TimeSpan.FromMilliseconds(1), holding: true);
        Assert.Equal(5, pump.Ticks);
        pump.Update(Tick(6) + TimeSpan.FromMilliseconds(2), holding: false);
        Assert.Equal(6, pump.Ticks);
        pump.Update(Tick(9), holding: false);
        Assert.Equal(9, pump.Ticks);
    }

    [Fact]
    public void TheBarAndItsWarningsStopThroughAHoldAndTakeOneTickAfterIt()
    {
        var pump = new EventPumpClock();
        var timer = new PlanningTimer();
        // A 30-second turn whose last ten seconds start at tick 121 (20086 ms).
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        pump.Update(TimeSpan.Zero, holding: false);
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.Zero, pump.Ticks));
        for (var tick = 1; tick <= 120; tick++)
        {
            pump.Update(Tick(tick), holding: false);
            timer.Advance(Tick(tick), pump.Ticks);
        }
        Assert.Equal(PlanningTimerPolicy.VisibleBarWidth(30000, (int)Tick(120).TotalMilliseconds),
            timer.VisibleBarWidth);
        var heldWidth = timer.VisibleBarWidth;

        // Held from tick 121 through tick 150: no redraw and no warning, though the clock is in
        // the warning range and passes five redraws.
        for (var tick = 121; tick <= 150; tick++)
        {
            pump.Update(Tick(tick), holding: true);
            Assert.Equal(PlanningTimerSignal.None, timer.Advance(Tick(tick), pump.Ticks));
            Assert.Equal(heldWidth, timer.VisibleBarWidth);
        }

        // Released: the countdown, at 6 after the redraw of tick 120, takes the one kept tick.
        pump.Update(Tick(150) + TimeSpan.FromMilliseconds(1), holding: false);
        Assert.Equal(PlanningTimerSignal.None,
            timer.Advance(Tick(150) + TimeSpan.FromMilliseconds(1), pump.Ticks));
        Assert.Equal(heldWidth, timer.VisibleBarWidth);
        for (var tick = 151; tick <= 154; tick++)
        {
            pump.Update(Tick(tick), holding: false);
            Assert.Equal(PlanningTimerSignal.None, timer.Advance(Tick(tick), pump.Ticks));
        }
        // Five ticks after the release the countdown runs out and the bar is redrawn from the
        // time that has really passed, with the warning that time calls for.
        pump.Update(Tick(155), holding: false);
        Assert.Equal(PlanningTimerSignal.LongWarning, timer.Advance(Tick(155), pump.Ticks));
        Assert.Equal(PlanningTimerPolicy.VisibleBarWidth(30000, (int)Tick(155).TotalMilliseconds),
            timer.VisibleBarWidth);
    }

    [Fact]
    public void TheBlinkAndTheSelectionFrameStopThroughAHold()
    {
        // comlink_blink_step steps in the pump, so the lights and the frame keep their phase.
        var pump = new EventPumpClock();
        pump.Update(Tick(4), holding: false);
        var lit = PresentationClock.BlinkLit(pump.Time);
        var frame = CityMapLayout.SelectionFrame(pump.Time);
        for (var tick = 5; tick <= 12; tick++)
        {
            pump.Update(Tick(tick), holding: true);
            Assert.Equal(lit, PresentationClock.BlinkLit(pump.Time));
            Assert.Equal(frame, CityMapLayout.SelectionFrame(pump.Time));
        }
    }

    [Fact]
    public void TheComlinkAlertRepeatWaitsOnThePump()
    {
        var pump = new EventPumpClock();
        var cadence = new ComlinkAlertCadence();
        pump.Update(TimeSpan.Zero, holding: false);
        Assert.True(cadence.Advance(true, true, pump.Time));
        // The first repeat is due at tick 24; a hold over it delays it.
        for (var tick = 1; tick <= 40; tick++)
        {
            pump.Update(Tick(tick), holding: tick is >= 20 and <= 40);
            Assert.False(cadence.Advance(true, true, pump.Time));
        }
        // Held from tick 20, the pump stopped at 19. Released at tick 41 it takes the kept tick,
        // 20, and four more before the repeat.
        for (var tick = 41; tick <= 45; tick++)
        {
            pump.Update(Tick(tick), holding: false);
            Assert.Equal(tick == 45, cadence.Advance(true, true, pump.Time));
        }
    }

    [Fact]
    public void ASoundtrackFadeKeepsThePumpFromRunning()
    {
        // FND-AUDIO-017: the fade runs inside the pump's music step and leaves timer slot 0 alone.
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Assert.False(OutsideEventPump(game));
        Field("_soundtrackFade").SetValue(game, new SoundtrackFade(1f, TimeSpan.Zero));
        Assert.True(OutsideEventPump(game));
        Assert.False(Holds(game));
        Field("_soundtrackFade").SetValue(game, null);
        Assert.False(OutsideEventPump(game));
    }

    [Fact]
    public void TheHoldsThatKeepThePumpFromRunning()
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Assert.False(Holds(game));
        AssertHolds(game, "_draggedHireDefinitionId", (short)1);
        AssertHolds(game, "_pressedHireRejectSlot", 0);
        AssertHolds(game, "_draggedGangId", new GangId(0));
        AssertHolds(game, "_pressedCityConsoleControl", CityConsoleControl.Done);
        AssertHolds(game, "_pressedCommandPanelButton", CommandPanelButton.Confirm);
        AssertHolds(game, "_pressedEventsButton", LastTurnEventsButton.Next);
        AssertHolds(game, "_pressedComlinkSendButton", FirstValueOf("_pressedComlinkSendButton"));
        AssertHolds(game, "_pressedAttackFace", FirstValueOf("_pressedAttackFace"));

        // The back control's helper loop follows the left button, so a right-button hold of it,
        // which only the rebuild keeps, lets the pump run.
        Field("_pressedPanelFace").SetValue(game,
            (SectorDetailLayout.Back, ClientScreen.Sector, (Action)(() => { })));
        Field("_pressedPanelFaceByRightButton").SetValue(game, true);
        Assert.False(Holds(game));
        Field("_pressedPanelFaceByRightButton").SetValue(game, false);
        Assert.True(Holds(game));
        Field("_pressedPanelFace").SetValue(game, null);
        Assert.False(Holds(game));
    }

    private static void AssertHolds(ChaosGame game, string field, object value)
    {
        Field(field).SetValue(game, value);
        Assert.True(Holds(game));
        Field(field).SetValue(game, null);
        Assert.False(Holds(game));
    }

    /// <summary>A value of the private enum a nullable field holds.</summary>
    private static object FirstValueOf(string field) =>
        Enum.GetValues(Nullable.GetUnderlyingType(Field(field).FieldType)
            ?? throw new InvalidOperationException(field)).GetValue(0)!;

    private static bool Holds(ChaosGame game) => Invoke(game, "HoldsPointerOutsideEventPump");

    private static bool OutsideEventPump(ChaosGame game) => Invoke(game, "OutsideEventPump");

    private static bool Invoke(ChaosGame game, string method) => (bool)(typeof(ChaosGame)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(ChaosGame), method))
        .Invoke(game, null)!;

    private static FieldInfo Field(string name) => typeof(ChaosGame)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(ChaosGame), name);
}
