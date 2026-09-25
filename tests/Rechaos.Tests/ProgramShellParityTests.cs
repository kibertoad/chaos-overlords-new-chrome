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
        // RULE-UI-008, FND-UI-024: the strip is copied for six ticks and filled black for two.
        Assert.Equal(new Rectangle(269, 169, 97, 9), IdleGangWarningLayout.BlinkingLine);
        for (var tick = 0; tick < 16; tick++)
            Assert.Equal(tick % 8 < 6,
                IdleGangWarningLayout.LineShown(TimeSpan.FromMilliseconds(tick * 166 + 80)));
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

    [Theory]
    [InlineData(0, 0)]
    [InlineData(639, 459)]
    [InlineData(320, 100)]
    [InlineData(500, 300)]
    public void ALeftPressOutsideTheTitleButtonsStartsANewGame(int x, int y)
    {
        // RULE-UI-013, SCR-UI-001: a left press anywhere on the title acts as New Game.
        Assert.Equal(ChaosGame.TitleAction.NewGame, ChaosGame.TitleActionAt(new Point(x, y)));
    }

    [Fact]
    public void TheRebuildsTitleButtonsKeepTheirCommands()
    {
        Assert.Equal(ChaosGame.TitleAction.NewGame, ChaosGame.TitleActionAt(new Point(230, 300)));
        Assert.Equal(ChaosGame.TitleAction.LoadGame, ChaosGame.TitleActionAt(new Point(230, 340)));
        Assert.Equal(ChaosGame.TitleAction.Quit, ChaosGame.TitleActionAt(new Point(410, 380)));
    }

    [Fact]
    public void ShiftChangesOnlyTheThirteenKeysOfTheOriginalsTable()
    {
        // RULE-UI-014: Shift gives the United States shifted character for the digits and
        // ' , . / ; =, and letters always come out in upper case.
        Assert.True(OriginalTextInput.TryCharacter(Keys.OemMinus, shift: true, out var minus));
        Assert.Equal('-', minus);
        Assert.True(OriginalTextInput.TryCharacter(Keys.OemPlus, shift: true, out var plus));
        Assert.Equal('+', plus);
        Assert.True(OriginalTextInput.TryCharacter(Keys.D2, shift: true, out var at));
        Assert.Equal('@', at);
        Assert.True(OriginalTextInput.TryCharacter(Keys.Q, shift: false, out var letter));
        Assert.Equal('Q', letter);
    }
}
