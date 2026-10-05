using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The program shell and presentation rules: the pointer (RULE-UI-007), the presentation clock
/// (RULE-UI-008), the title loop (RULE-UI-013) and the key translation of the event step
/// (RULE-UI-014).
/// </summary>
public sealed class ProgramShellParityTests
{
    [Fact]
    public void PresentationClockTicksEvery166MillisecondsFromOnePhase()
    {
        // RULE-UI-008: the period is 1000 / 6 in integer arithmetic.
        Assert.Equal(166, PresentationClock.PeriodMilliseconds);
        Assert.Equal(TimeSpan.FromMilliseconds(166), PresentationClock.Period);
        Assert.Equal(0, PresentationClock.Ticks(TimeSpan.FromMilliseconds(165)));
        Assert.Equal(1, PresentationClock.Ticks(TimeSpan.FromMilliseconds(166)));
        Assert.Equal(6, PresentationClock.Ticks(TimeSpan.FromMilliseconds(996)));
        Assert.Equal(5, PresentationClock.Ticks(TimeSpan.FromMilliseconds(995)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationClock.Ticks(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void ConsoleLightsAreLitForTwoTicksAndDarkForTwo()
    {
        // SCR-UI-003 through RULE-UI-008: two ticks lit, two dark.
        bool[] expected = [true, true, false, false, true, true, false, false];
        for (var tick = 0; tick < expected.Length; tick++)
        {
            var start = TimeSpan.FromMilliseconds(tick * 166);
            Assert.Equal(expected[tick], PresentationClock.BlinkLit(start));
            Assert.Equal(expected[tick], PresentationClock.BlinkLit(start + TimeSpan.FromMilliseconds(165)));
        }
    }

    [Fact]
    public void PresentationReadersShareTheOneClock()
    {
        // RULE-UI-008: combat frames, the Comlink caret and the item rotation step on its ticks.
        Assert.Equal(PresentationClock.PeriodMilliseconds, CombatAnimationRouting.FrameMilliseconds);
        Assert.Equal(PresentationClock.Period, ComlinkCaretCadence.TimerEventInterval);
        Assert.Equal(PresentationClock.Period * 24, ComlinkAlertCadence.RepeatInterval);
        Assert.Equal(ItemRotationPresentation.Frame(0),
            ItemRotationPresentation.Frame(TimeSpan.FromMilliseconds(165)));
        Assert.Equal(ItemRotationPresentation.Frame(1),
            ItemRotationPresentation.Frame(TimeSpan.FromMilliseconds(166)));
        // SCR-UI-006: fifteen frames take 2.5 seconds, less the integer period's rounding.
        Assert.Equal(ItemRotationPresentation.Frame(0),
            ItemRotationPresentation.Frame(TimeSpan.FromMilliseconds(15 * 166)));
    }

    [Fact]
    public void IdleGangWarningLineShowsForSixTicksAndGoesBlackForTwo()
    {
        // RULE-UI-008, FND-UI-024, FND-UI-054: from the open, the strip is copied for six ticks
        // and filled black for two.
        Assert.Equal(new Rectangle(269, 169, 97, 9), IdleGangWarningLayout.BlinkingLine);
        for (var tick = 0; tick < 16; tick++)
            Assert.Equal(tick % 8 < 6, IdleGangWarningLayout.LineShown(tick));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdleGangWarningLayout.LineShown(-1));
        // FND-UI-054: the count starts at the open, so a panel opened where the program's own
        // clock stands at tick 6 still starts with the line shown. FND-TIMER-002: the ticks are
        // the program's own, so the sixth falls at the clock's tick 12, less than six periods
        // after the open.
        var opened = TimeSpan.FromMilliseconds(6 * 166 + 50);
        Assert.True(IdleGangWarningLayout.LineShown(opened, TimeSpan.FromMilliseconds(12 * 166 - 1)));
        Assert.False(IdleGangWarningLayout.LineShown(opened, TimeSpan.FromMilliseconds(12 * 166)));
        Assert.True(IdleGangWarningLayout.LineShown(opened, opened));
        Assert.True(IdleGangWarningLayout.LineShown(opened, opened + TimeSpan.FromMilliseconds(5 * 166 + 80)));
        Assert.False(IdleGangWarningLayout.LineShown(opened, opened + TimeSpan.FromMilliseconds(6 * 166 + 80)));
        Assert.True(IdleGangWarningLayout.LineShown(opened, opened + TimeSpan.FromMilliseconds(8 * 166 + 80)));
        Assert.True(IdleGangWarningLayout.LineShown(opened, opened - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void BusyWorkShowsTheHourglassAndThenTheArrow()
    {
        // RULE-UI-007: the hourglass around setup, loading and resolution, the arrow after.
        var shown = new List<PointerShape>();
        var pointer = new PresentationPointer(shown.Add);

        using (pointer.Busy())
        {
            Assert.Equal([PointerShape.Hourglass], shown);
            using (pointer.Busy())
            {
            }
            Assert.Equal([PointerShape.Hourglass], shown);
        }
        Assert.Equal([PointerShape.Hourglass, PointerShape.Arrow], shown);

        shown.Clear();
        Assert.Throws<InvalidOperationException>(FailWhileBusy);
        Assert.Equal([PointerShape.Hourglass, PointerShape.Arrow], shown);

        void FailWhileBusy()
        {
            using var busy = pointer.Busy();
            throw new InvalidOperationException();
        }
    }

    [Fact]
    public void TheHourglassStaysUpWhileAComputersPlanningWaits()
    {
        // RULE-UI-007, EXP-UI-022: between updates the pointer shows the idle shape, and a busy
        // scope that ends while a computer's planning waits leaves the hourglass up.
        var shown = new List<PointerShape>();
        var idle = PointerShape.Hourglass;
        var pointer = new PresentationPointer(shown.Add, () => idle);

        using (pointer.Busy())
        {
        }
        pointer.Refresh();
        Assert.Equal([PointerShape.Hourglass], shown);

        idle = PointerShape.Arrow;
        using (pointer.Busy())
            pointer.Refresh();
        Assert.Equal([PointerShape.Hourglass, PointerShape.Arrow], shown);
        pointer.Refresh();
        Assert.Equal([PointerShape.Hourglass, PointerShape.Arrow], shown);
    }

    [Fact]
    public void APointerMovedWhileTheHourglassWaitsShowsTheArrowUntilTheNextWork()
    {
        // RULE-UI-007, FND-AUDIO-016: a music fade dispatches messages while a computer's planning
        // waits, and each pointer message selects the arrow until the planning selects the
        // hourglass again.
        var shown = new List<PointerShape>();
        var idle = PointerShape.Hourglass;
        var pointer = new PresentationPointer(shown.Add, () => idle);

        pointer.Refresh();
        pointer.PointerMoved();
        pointer.Refresh();
        Assert.Equal([PointerShape.Hourglass, PointerShape.Arrow], shown);

        using (pointer.Busy())
        {
        }
        pointer.Refresh();
        Assert.Equal([PointerShape.Hourglass, PointerShape.Arrow, PointerShape.Hourglass], shown);

        // A move while the idle shape is the arrow is spent once the hourglass waits again.
        idle = PointerShape.Arrow;
        pointer.Refresh();
        pointer.PointerMoved();
        pointer.Refresh();
        idle = PointerShape.Hourglass;
        pointer.Refresh();
        Assert.Equal(
            [PointerShape.Hourglass, PointerShape.Arrow, PointerShape.Hourglass, PointerShape.Arrow, PointerShape.Hourglass],
            shown);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(639, 459)]
    [InlineData(320, 100)]
    [InlineData(500, 300)]
    public void ALeftPressOutsideTheTitleButtonsDoesNothing(int x, int y)
    {
        // RULE-UI-013, SCR-UI-001: the original's press anywhere acts as New Game. DEV-UI-019: the
        // rebuild's title is its menu, and only its buttons act.
        Assert.Null(ChaosGame.TitleActionAt(new Point(x, y)));
    }

    [Theory]
    [InlineData(Keys.N, true, "NewGame")]
    [InlineData(Keys.O, true, "LoadGame")]
    [InlineData(Keys.H, true, "Online")]
    [InlineData(Keys.J, true, "Online")]
    [InlineData(Keys.N, false, null)]
    [InlineData(Keys.O, false, null)]
    [InlineData(Keys.Enter, false, "NewGame")]
    [InlineData(Keys.F9, false, "LoadGame")]
    public void TitleKeysFollowScrUi001(Keys key, bool control, string? expected)
    {
        // SCR-UI-001: Ctrl+N, Ctrl+O, Ctrl+H and Ctrl+J act as the menu items; the rebuild's
        // Online screen holds Host and Join (DEV-UI-019).
        Assert.Equal(expected, ChaosGame.TitleShortcut(key, control)?.ToString());
    }

    [Fact]
    public void TheRebuildsTitleButtonsKeepTheirCommands()
    {
        Assert.Equal(ChaosGame.TitleAction.NewGame, ChaosGame.TitleActionAt(new Point(230, 300)));
        Assert.Equal(ChaosGame.TitleAction.LoadGame, ChaosGame.TitleActionAt(new Point(230, 340)));
        Assert.Equal(ChaosGame.TitleAction.Quit, ChaosGame.TitleActionAt(new Point(410, 380)));
    }

    [Fact]
    public void ShiftChangesOnlyTheSixteenKeysOfTheOriginalsTable()
    {
        // RULE-UI-014, FND-UI-020: with Shift held the sixteen keys of the original's table give
        // the United States shifted character, minus keeps its own, and letters always come out
        // in upper case.
        (Keys Key, char Plain, char Shifted)[] table =
        [
            (Keys.OemQuotes, '\'', '"'), (Keys.OemComma, ',', '<'), (Keys.OemPeriod, '.', '>'),
            (Keys.OemQuestion, '/', '?'), (Keys.OemSemicolon, ';', ':'), (Keys.OemPlus, '=', '+'),
            (Keys.D0, '0', ')'), (Keys.D1, '1', '!'), (Keys.D2, '2', '@'), (Keys.D3, '3', '#'),
            (Keys.D4, '4', '$'), (Keys.D5, '5', '%'), (Keys.D6, '6', '^'), (Keys.D7, '7', '&'),
            (Keys.D8, '8', '*'), (Keys.D9, '9', '('),
            (Keys.OemMinus, '-', '-')
        ];
        foreach (var (key, plain, shifted) in table)
        {
            Assert.True(OriginalTextInput.TryCharacter(key, shift: false, out var unshifted));
            Assert.Equal(plain, unshifted);
            Assert.True(OriginalTextInput.TryCharacter(key, shift: true, out var withShift));
            Assert.Equal(shifted, withShift);
        }
        for (var key = Keys.A; key <= Keys.Z; key++)
        {
            Assert.True(OriginalTextInput.TryCharacter(key, shift: false, out var letter));
            Assert.Equal((char)('A' + (key - Keys.A)), letter);
            Assert.True(OriginalTextInput.TryCharacter(key, shift: true, out var shiftedLetter));
            Assert.Equal(letter, shiftedLetter);
        }
    }

    [Fact]
    public void ClosingTheWindowWithNoMatchInPlayQuitsAtOnce()
    {
        // RULE-UI-013, RULE-UI-014: a closed window is File, Exit, which on the title, or once the
        // match is no longer in play, sets quit_requested without asking.
        var game = DeviationBehaviourTests.HeadlessGame();
        Assert.False(OriginalNewGameExperimentTests.ClosingIsCancelled(game));
    }
}
