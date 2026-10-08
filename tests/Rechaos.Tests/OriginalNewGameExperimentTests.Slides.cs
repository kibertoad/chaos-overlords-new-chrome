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

    // FND-UI-066: the return address of each call of the original's panel-open helper names the
    // handler that opened the panel. Each entry gives the rebuild's screen for that panel, the travel
    // of the form the handler passes (FND-UI-011), how the rebuild's transition starts it, and how a
    // test opens it in the game: the steps before the opening key, and the key.
    private sealed record SlideCaller(
        ClientScreen Screen, int Travel, Action<PanelSlideTransition, TimeSpan> Begin,
        Action<HeadlessGame> Prepare, Keys Opening);

    private static readonly IReadOnlyDictionary<int, SlideCaller> SlideCallers = new Dictionary<int, SlideCaller>
    {
        // FND-UI-066: the Hire comparison fn_004546C5, in the alternate form, opened from the
        // city's H key.
        [0x00454D2A] = new(ClientScreen.Hire, PanelSlideTransition.AlternateStartOffset,
            (transition, now) => transition.Begin(ClientScreen.City, ClientScreen.Hire, now),
            _ => { }, Keys.H),
        // FND-UI-066: the Move panel fn_004413EF, in the primary form, opened from the command
        // overlay's Move entry (ChaosGame.ActivateCommandSelection).
        [0x00441A99] = new(ClientScreen.Commands, PanelSlideTransition.StartOffset,
            (transition, now) => transition.BeginOrderPanel(ClientScreen.Sector, now),
            game => OpenOrderList(game, GangAction.Move), Keys.Enter),
    };

    // The rebuild's panel for a recorded slide, through the handler its caller names (FND-UI-066).
    // A slide whose caller has no entry fails: the run reached a panel this table does not map yet.
    private static SlideCaller CallerOf(RecordedSlide slide)
    {
        Assert.True(SlideCallers.TryGetValue(slide.Caller, out var caller),
            $"No rebuild panel is mapped for the slide from the call returning to 0x{slide.Caller:X8} (FND-UI-066).");
        Assert.Equal(caller!.Travel, slide.Travel);
        return caller;
    }

    // RULE-UI-003, FND-UI-011: the probe recorded each copy of the original's slide-ins and the
    // call each came from (EXP-UI-025, FND-UI-066), the Hire panel in the alternate form and the
    // Move panel in the primary form. The rebuild's step and copy sequence for the benchmark count
    // the original read give the same offsets. DEV-TIMER-001: the rebuild paces the copies at its
    // fixed 84 a second, which also takes the 16-pixel step, and the screen the caller maps to
    // shows the same copies in order.
    [Theory]
    [MemberData(nameof(SlideRuns))]
    public void PanelsSlideInWithTheOriginalsCopies(string experiment, int run)
    {
        foreach (var slide in Run(experiment, run).Slides!)
        {
            Assert.Equal(PanelSlideTransition.SlideInOffsets(slide.Travel,
                PanelSlideTransition.SlideStep(slide.Travel, slide.Benchmark), slidePanels: true), slide.Offsets);

            var caller = CallerOf(slide);
            var transition = new PanelSlideTransition();
            var start = TimeSpan.FromSeconds(10);
            caller.Begin(transition, start);
            var shown = new List<int>();
            for (var copy = 0; copy <= slide.Offsets.Count; copy++)
            {
                var offset = transition.Offset(caller.Screen, start + TimeSpan.FromTicks(
                    copy * TimeSpan.TicksPerSecond / PanelSlideTransition.NominalBlitBenchmarkCount + 1));
                if (shown.Count == 0 || shown[^1] != offset) shown.Add(offset);
                if (offset == 0) break;
            }
            Assert.Equal(slide.Offsets, shown);
        }
    }

    // RULE-UI-003, FND-UI-011, FND-UI-057: with Slide Panels on, opening in the rebuild the panel
    // each recorded slide's caller maps to (FND-UI-066) plays slot 0 on the update that opens it and
    // starts the slide there, and the game's own transition shows the recorded copies at its fixed
    // pace (DEV-TIMER-001).
    [Theory]
    [MemberData(nameof(SlideRuns))]
    public void TheGameSlidesItsPanelsInWithTheOriginalsCopies(string experiment, int run)
    {
        foreach (var slide in Run(experiment, run).Slides!)
        {
            var caller = CallerOf(slide);
            using var game = new HeadlessGame(HeadlessGame.DefaultPreferences with { SlidePanels = true });
            game.StartLocalMatch();
            Assert.Equal(ClientScreen.City, game.Game.CurrentScreen);
            caller.Prepare(game);
            game.Sounds.Clear();
            game.HoldKey(caller.Opening);
            var start = game.Game.InputTime;
            Assert.Equal(caller.Screen, game.Game.CurrentScreen);
            if (caller.Screen == ClientScreen.Commands) Assert.True(game.Game.OrderPanelOpen);
            Assert.Equal([GeneralSoundSlot.PanelOpen], game.Sounds.GeneralSlots);

            var shown = new List<int>();
            for (var copy = 0; copy <= slide.Offsets.Count; copy++)
            {
                var offset = game.Game.PanelSlide.Offset(caller.Screen, start + TimeSpan.FromTicks(
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
