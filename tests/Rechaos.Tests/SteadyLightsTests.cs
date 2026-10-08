using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// DEV-UI-027: with Steady Lights on, the console lights stay lit, and the selected sector's
/// frame, the Overlord bar's marker and empty-seat art, the Comlink Send caret and the rotating
/// item pictures hold one frame at every tick; off, they blink and cycle on the presentation clock
/// as SCR-UI-003, SCR-COMLINK-002 and SCR-UI-006 record. The setting starts off.
/// </summary>
public sealed class SteadyLightsTests
{
    private static readonly TimeSpan[] Times =
        Enumerable.Range(0, 16).Select(tick => PresentationClock.Period * tick).ToArray();

    [Fact]
    public void OffTheLightsBlinkAndTheSelectionFrameCycles()
    {
        var game = GameAt(steady: false);

        var lit = Times.Select(time => Lit(game, time)).ToArray();
        var frames = Times.Select(time => SelectionFrame(game, time)).ToArray();

        Assert.Contains(true, lit);
        Assert.Contains(false, lit);
        Assert.Contains(0, frames);
        Assert.Contains(1, frames);
    }

    [Fact]
    public void OnTheLightsStayLitAndTheSelectionFrameHolds()
    {
        var game = GameAt(steady: true);

        Assert.All(Times, time => Assert.True(Lit(game, time)));
        Assert.All(Times, time => Assert.Equal(0, SelectionFrame(game, time)));
    }

    [Theory]
    [InlineData("MarkerFrameShown")]
    [InlineData("EmptySeatFrameShown")]
    public void TheOverlordBarTurnsOffAndHoldsItsFirstFrameOn(string method)
    {
        var off = GameAt(steady: false);
        var on = GameAt(steady: true);

        Assert.True(Times.Select(time => OverlordBarFrame(off, method, time)).Distinct().Count() > 1);
        Assert.All(Times, time => Assert.Equal(0, OverlordBarFrame(on, method, time)));
    }

    [Fact]
    public void TheComlinkCaretFlipsOffAndStaysInverseOn()
    {
        var off = GameAt(steady: false);
        var on = GameAt(steady: true);

        Assert.Equal(new[] { false, true }, Times.Select(time => CaretInverse(off, time)).Distinct().Order());
        Assert.All(Times, time => Assert.True(CaretInverse(on, time)));
    }

    [Fact]
    public void TheItemPicturesTurnOffAndHoldTheirFirstFrameOn()
    {
        var off = GameAt(steady: false);
        var on = GameAt(steady: true);
        var first = ItemRotationPresentation.FrameAfter(0);

        Assert.True(Enumerable.Range(0, 16).Select(ticks => ItemFrame(off, ticks)).Distinct().Count() > 1);
        Assert.All(Enumerable.Range(0, 16), ticks => Assert.Equal(first, ItemFrame(on, ticks)));
    }

    [Fact]
    public void ARecordedLampPhaseStillWins()
    {
        // The reference frame passes the phase a capture recorded, which the setting never
        // overrides; captures are compared with every setting off.
        var game = GameAt(steady: true);

        Assert.False((bool)DeviationBehaviourTests.Call(game, "LampInLitPhase", (bool?)false)!);
    }

    [Fact]
    public void TheSettingStartsOff()
    {
        Assert.False(OriginalOptionsPolicy.SteadyLightsByDefault);
        Assert.False(GamePreferences.Default.SteadyLights);
    }

    private static ChaosGame GameAt(bool steady)
    {
        var game = DeviationBehaviourTests.HeadlessGame();
        DeviationBehaviourTests.Field("_steadyLights").SetValue(game, steady);
        return game;
    }

    private static void Advance(ChaosGame game, TimeSpan time) =>
        ((EventPumpClock)DeviationBehaviourTests.Field("_eventPump").GetValue(game)!).Update(time, holding: false);

    private static bool Lit(ChaosGame game, TimeSpan time)
    {
        Advance(game, time);
        return (bool)DeviationBehaviourTests.Call(game, "LampInLitPhase", (bool?)null)!;
    }

    private static int SelectionFrame(ChaosGame game, TimeSpan time)
    {
        Advance(game, time);
        return (int)DeviationBehaviourTests.Call(game, "SelectionFrameShown")!;
    }

    private static int OverlordBarFrame(ChaosGame game, string method, TimeSpan time)
    {
        DeviationBehaviourTests.Field("_inputTime").SetValue(game, time);
        return (int)DeviationBehaviourTests.Call(game, method)!;
    }

    private static bool CaretInverse(ChaosGame game, TimeSpan time)
    {
        ((ComlinkCaretCadence)DeviationBehaviourTests.Field("_comlinkCaretCadence").GetValue(game)!).Advance(time);
        return (bool)DeviationBehaviourTests.Call(game, "get_ComlinkCaretInverse")!;
    }

    private static Rectangle ItemFrame(ChaosGame game, long ticks) =>
        (Rectangle)DeviationBehaviourTests.Call(game, "ItemRotationFrameAfter", ticks)!;
}
