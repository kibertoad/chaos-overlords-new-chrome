using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> SlideRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].Slides is { Count: > 0 })
                    data.Add(experiment, run);
        return data;
    }

    // RULE-UI-003, FND-UI-011: the probe recorded each copy of the original's slide-ins
    // (EXP-UI-025), the Hire panel in the alternate form and the Move panel in the primary form. The
    // rebuild's step and copy sequence for the benchmark count the original read give the same
    // offsets. DEV-TIMER-001: the rebuild paces the copies at its fixed 84 a second, which also
    // takes the 16-pixel step, and its Hire screen and order panels show the same copies in order.
    [Theory]
    [MemberData(nameof(SlideRuns))]
    public void PanelsSlideInWithTheOriginalsCopies(string experiment, int run)
    {
        foreach (var slide in Run(experiment, run).Slides!)
        {
            Assert.Equal(PanelSlideTransition.SlideInOffsets(slide.Travel,
                PanelSlideTransition.SlideStep(slide.Travel, slide.Benchmark), slidePanels: true), slide.Offsets);

            var transition = new PanelSlideTransition();
            var start = TimeSpan.FromSeconds(10);
            ClientScreen screen;
            if (slide.Travel == PanelSlideTransition.AlternateStartOffset)
            {
                screen = ClientScreen.Hire;
                transition.Begin(ClientScreen.City, screen, start);
            }
            else
            {
                Assert.Equal(PanelSlideTransition.StartOffset, slide.Travel);
                screen = ClientScreen.Commands;
                transition.BeginOrderPanel(ClientScreen.Sector, start);
            }
            var shown = new List<int>();
            for (var copy = 0; copy <= slide.Offsets.Count; copy++)
            {
                var offset = transition.Offset(screen, start + TimeSpan.FromTicks(
                    copy * TimeSpan.TicksPerSecond / PanelSlideTransition.NominalBlitBenchmarkCount + 1));
                if (shown.Count == 0 || shown[^1] != offset) shown.Add(offset);
                if (offset == 0) break;
            }
            Assert.Equal(slide.Offsets, shown);
        }
    }

    // RULE-UI-003, FND-UI-011, FND-UI-057: with Slide Panels on, opening the panel each recorded
    // slide came from in the rebuild plays slot 0 on the update that opens it and starts the slide
    // there, and the game's own transition shows the recorded copies at its fixed pace
    // (DEV-TIMER-001). The Hire panel opens from the city's H key; an order panel opens from the
    // command overlay's Move entry (ChaosGame.ActivateCommandSelection).
    [Theory]
    [MemberData(nameof(SlideRuns))]
    public void TheGameSlidesItsPanelsInWithTheOriginalsCopies(string experiment, int run)
    {
        foreach (var slide in Run(experiment, run).Slides!)
        {
            using var game = new HeadlessGame(HeadlessGame.DefaultPreferences with { SlidePanels = true });
            game.StartLocalMatch();
            Assert.Equal(ClientScreen.City, game.Game.CurrentScreen);
            ClientScreen screen;
            Keys opening;
            if (slide.Travel == PanelSlideTransition.AlternateStartOffset)
            {
                screen = ClientScreen.Hire;
                opening = Keys.H;
            }
            else
            {
                Assert.Equal(PanelSlideTransition.StartOffset, slide.Travel);
                screen = ClientScreen.Commands;
                opening = Keys.Enter;
                OpenOrderList(game, GangAction.Move);
            }
            game.Sounds.Clear();
            game.HoldKey(opening);
            var start = game.Game.InputTime;
            Assert.Equal(screen, game.Game.CurrentScreen);
            if (screen == ClientScreen.Commands) Assert.True(game.Game.OrderPanelOpen);
            Assert.Equal([GeneralSoundSlot.PanelOpen], game.Sounds.GeneralSlots);

            var shown = new List<int>();
            for (var copy = 0; copy <= slide.Offsets.Count; copy++)
            {
                var offset = game.Game.PanelSlide.Offset(screen, start + TimeSpan.FromTicks(
                    copy * TimeSpan.TicksPerSecond / PanelSlideTransition.NominalBlitBenchmarkCount + 1));
                if (shown.Count == 0 || shown[^1] != offset) shown.Add(offset);
                if (offset == 0) break;
            }
            Assert.Equal(slide.Offsets, shown);
        }
    }

    // RULE-UI-003, FND-UI-057: Back on an order panel goes back to the order list, which never
    // slides, so a slide still running stops there, and the close helper plays slot 1 after the
    // key's own slot 3. With Slide Panels off the panel opens in place, silently.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BackFromAnOrderPanelStopsItsSlideAndPlaysTheCloseSound(bool slidePanels)
    {
        using var game = new HeadlessGame(HeadlessGame.DefaultPreferences with { SlidePanels = slidePanels });
        game.StartLocalMatch();
        OpenOrderList(game, GangAction.Move);
        game.Sounds.Clear();
        game.Press(Keys.Enter);
        Assert.True(game.Game.OrderPanelOpen);
        var sliding = game.Game.PanelSlide.Offset(ClientScreen.Commands, game.Now);
        if (slidePanels)
        {
            Assert.InRange(sliding, 1, PanelSlideTransition.StartOffset - 1);
            Assert.Equal([GeneralSoundSlot.PanelOpen], game.Sounds.GeneralSlots);
        }
        else
        {
            Assert.Equal(0, sliding);
            Assert.DoesNotContain(GeneralSoundSlot.PanelOpen, game.Sounds.GeneralSlots);
        }

        game.Sounds.Clear();
        game.Press(Keys.Back);
        Assert.Equal(ClientScreen.Commands, game.Game.CurrentScreen);
        Assert.False(game.Game.OrderPanelOpen);
        Assert.Equal(0, game.Game.PanelSlide.Offset(ClientScreen.Commands, game.Now));
        Assert.Equal(
            slidePanels
                ? [GeneralSoundSlot.AcceptedSelection, GeneralSoundSlot.PanelClose]
                : [GeneralSoundSlot.AcceptedSelection],
            game.Sounds.GeneralSlots);
    }

    /// <summary>Opens the command overlay of the selected gang from the city with its cursor on <paramref name="order"/>.</summary>
    private static void OpenOrderList(HeadlessGame game, GangAction order)
    {
        game.Press(Keys.C);
        Assert.Equal(ClientScreen.Commands, game.Game.CurrentScreen);
        Assert.False(game.Game.OrderPanelOpen);
        for (var step = 0; step < 32 && game.Game.CommandAtCursor != order; step++) game.Press(Keys.Down);
        Assert.Equal(order, game.Game.CommandAtCursor);
    }
}
