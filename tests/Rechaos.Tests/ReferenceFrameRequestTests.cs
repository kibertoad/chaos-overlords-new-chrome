using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ReferenceFrameRequestTests
{
    [Fact]
    public void OrdinaryStartupHasNoCaptureRequest() =>
        Assert.Null(ReferenceFrameRequest.ParseArguments(["--assets", "assets"]));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void CaptureUsesAbsolutePathsAndAnIsolatedDataDirectory(int frame)
    {
        // FND-UI-038: both ends of the twelve-frame marker range are valid.
        var request = Assert.IsType<ReferenceFrameRequest>(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--marker-frame", frame.ToString()]));
        Assert.Equal(Path.GetFullPath("planning.rchsave"), request.SavePath);
        Assert.Equal(Path.GetFullPath("frame.bmp"), request.OutputPath);
        Assert.Equal(frame, request.MarkerFrame);
        Assert.StartsWith(Path.Combine(Path.GetDirectoryName(request.OutputPath)!, "rechaos-reference-frame-"), request.UserDataDirectory);
        var next = new ReferenceFrameRequest(request.SavePath, request.OutputPath);
        Assert.NotEqual(request.UserDataDirectory, next.UserDataDirectory);
        Assert.Null(next.MarkerFrame);
    }

    // EXP-UI-036: an endpoint capture stands at the probe's dump, before the planning entry's Last
    // Turn Events is exited; a shot step stands after it.
    [Fact]
    public void EntryPanelsAreAskedForByAFlag()
    {
        Assert.True(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--entry-panels"])!.EntryPanels);
        Assert.False(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp"])!.EntryPanels);
    }

    // RULE-OPTIONS-003: the probe switches Warn if Idle Gangs off in a run that presses Done.
    [Fact]
    public void TheIdleGangWarningCanBeSwitchedOff()
    {
        Assert.False(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--no-idle-warning"])!.IdleGangWarning);
        Assert.True(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp"])!.IdleGangWarning);
    }

    // SCR-UI-001, SCR-UI-002, SCR-SETUP-001: a screen's name in place of the save asks for that
    // screen.
    [Theory]
    [InlineData("title")]
    [InlineData("credits")]
    [InlineData("setup")]
    public void AScreenOperandNamesNoSave(string screen)
    {
        var request = Assert.IsType<ReferenceFrameRequest>(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", screen, "frame.bmp"]));
        Assert.Equal(screen, request.Screen);
        Assert.Equal(screen, request.SavePath);
        Assert.Null(ReferenceFrameRequest.ParseArguments(["--reference-frame", "planning.rchsave", "frame.bmp"])!.Screen);
    }

    public static TheoryData<string[]> InvalidRequests => new()
    {
        new[] { "--reference-frame" },
        new[] { "--reference-frame", "save" },
        new[] { "--reference-frame", "save", "--assets", "assets" },
        new[] { "--marker-frame", "0" },
        new[] { "--reference-frame", "save", "frame", "--marker-frame" },
        new[] { "--reference-frame", "save", "frame", "--marker-frame", "-1" },
        new[] { "--reference-frame", "save", "frame", "--marker-frame", "12" },
        new[] { "--reference-frame", "save", "frame", "--marker-frame", "bad" },
        new[] { "--reference-frame", "save", "frame", "--reference-frame", "other", "other.bmp" },
        new[] { "--reference-frame", "save", "frame", "--marker-frame", "0", "--marker-frame", "1" },
        new[] { "--reference-frame", "save", "save" },
        new[] { "--reference-clicks", "1:2" },
        new[] { "--pump-counter", "1" },
        new[] { "--entry-panels" },
        new[] { "--no-idle-warning" },
        new[] { "--reference-frame", "title", "frame", "--entry-panels" },
        new[] { "--reference-frame", "save", "frame", "--entry-panels", "--entry-panels" },
        new[] { "--selected-sector", "1" },
        new[] { "--reference-frame", "save", "frame", "--selected-sector", "64" },
        new[] { "--reference-frame", "save", "frame", "--selected-sector", "-1" },
        new[] { "--item-frame", "1" },
        new[] { "--reference-frame", "save", "frame", "--item-frame", "15" },
        new[] { "--clip-tick", "1" },
        new[] { "--reference-frame", "save", "frame", "--clip-tick", "22" },
        new[] { "--reference-frame", "title", "frame", "--clip-tick", "1" },
        new[] { "--idle-phase", "1" },
        new[] { "--reference-frame", "save", "frame", "--idle-phase", "8" },
        new[] { "--caret-phase", "1" },
        new[] { "--reference-frame", "save", "frame", "--caret-phase", "6" },
        new[] { "--clip-index", "1" },
        new[] { "--reference-frame", "save", "frame", "--clip-index", "1" },
        new[] { "--reference-frame", "save", "frame", "--clip-tick", "1", "--clip-index", "-1" },
        new[] { "--reference-frame", "title", "frame", "--idle-phase", "1" },
        new[] { "--reference-frame", "save", "frame", "--item-frame", "1", "--item-frame", "2" },
        new[] { "--reference-frame", "title", "frame", "--marker-frame", "0" },
        new[] { "--reference-frame", "setup", "frame", "--selected-sector", "1" },
        new[] { "--reference-frame", "save", "frame", "--pump-counter", "8" },
        new[] { "--reference-frame", "save", "frame", "--pump-counter", "-1" },
        new[] { "--reference-frame", "save", "frame", "--pump-counter" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "1" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "640:0" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "0:460" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "1:2:3" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "-1:2" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "1:2", "--reference-clicks", "1:2" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "1:2>640:0" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "1:2:2>3:4" },
        new[] { "--reference-frame", "save", "frame", "--reference-clicks", "1:2>3:4>5:6" },
        new[] { "--lamps", "0,1" },
        new[] { "--reference-frame", "save", "frame", "--lamps" },
        new[] { "--reference-frame", "save", "frame", "--lamps", "1" },
        new[] { "--reference-frame", "save", "frame", "--lamps", "2,0" },
        new[] { "--reference-frame", "save", "frame", "--lamps", "0,1,0" },
        new[] { "--reference-frame", "save", "frame", "--lamps", "0,1", "--lamps", "0,1" },
    };

    [Theory]
    [InlineData("0,0", false, false)]
    [InlineData("1,0", true, false)]
    [InlineData("0,1", false, true)]
    [InlineData("1,1", true, true)]
    public void TheLampsGiveTheEventsAndComlinkPhases(string value, bool events, bool comlink)
    {
        // FND-EVENT-006: the bytes the pump sets while it has a lamp drawn lit.
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--lamps", value])!;
        Assert.Equal(new ReferenceLamps(events, comlink), request.Lamps);
        Assert.Equal(value, request.Lamps!.ToString());
        Assert.Null(ReferenceFrameRequest.ParseArguments(["--reference-frame", "planning.rchsave", "frame.bmp"])!.Lamps);
    }

    [Fact]
    public void ClicksAreReadInOrderWithTheirDoubleClicks()
    {
        var request = Assert.IsType<ReferenceFrameRequest>(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--reference-clicks", "600:58,137:301:2,639:459"]));
        Assert.Equal(
            [new ReferenceClick(new(600, 58)), new ReferenceClick(new(137, 301), Double: true), new ReferenceClick(new(639, 459))],
            request.Clicks!);
        Assert.Equal("600:58,137:301:2,639:459", string.Join(",", request.Clicks!));
        Assert.Null(ReferenceFrameRequest.ParseArguments(["--reference-frame", "planning.rchsave", "frame.bmp"])!.Clicks);
    }

    [Fact]
    public void ADragIsReadWithItsReleasePoint()
    {
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "setup", "frame.bmp", "--reference-clicks", "416:340>300:200,10:20"])!;
        Assert.Equal(
            [new ReferenceClick(new(416, 340), Release: new(300, 200)), new ReferenceClick(new(10, 20))],
            request.Clicks!);
        Assert.Equal("416:340>300:200,10:20", string.Join(",", request.Clicks!));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(7, 1)]
    public void ThePumpCounterPicksTheFrameDrawnBeforeIt(int counter, int frame)
    {
        // FND-UI-048, EXP-UI-006: the pump draws the frame, then advances the counter.
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--pump-counter", counter.ToString()])!;
        Assert.Equal(counter, request.PumpCounter);
        Assert.Equal(frame, CityMapLayout.SelectionFrameAfterPass(counter));
    }

    [Fact]
    public void TheSelectedSectorIsReadAsGiven()
    {
        // FND-SAVE-003, DEV-SAVE-001: the save keeps no selection, so the capture supplies it.
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--selected-sector", "12"])!;
        Assert.Equal(12, request.SelectedSector);
        Assert.Null(ReferenceFrameRequest.ParseArguments(["--reference-frame", "planning.rchsave", "frame.bmp"])!.SelectedSector);
    }

    [Fact]
    public void TheItemFrameIsReadAsGiven()
    {
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--item-frame", "14"])!;
        Assert.Equal(14, request.ItemFrame);
        Assert.Null(ReferenceFrameRequest.ParseArguments(["--reference-frame", "planning.rchsave", "frame.bmp"])!.ItemFrame);
    }

    // RULE-COMLINK-006: typed text runs between the clicks, as the Send panel takes keys.
    [Fact]
    public void TypedTextIsReadBetweenTheClicks()
    {
        var clicks = ReferenceClick.ParseList("576:166,'MEET ME 0700,250:190");
        Assert.Equal("MEET ME 0700", clicks[1].Text);
        Assert.Equal("576:166,'MEET ME 0700,250:190", string.Join(",", clicks));
        Assert.Throws<ArgumentException>(() => ReferenceClick.ParseList("'lower"));
        Assert.Throws<ArgumentException>(() => ReferenceClick.ParseList("'"));
    }

    // FND-COMBAT-016: the screen shows a clip's ticks 0 to 21.
    [Fact]
    public void TheClipTickIsReadAsGiven()
    {
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--clip-tick", "21"])!;
        Assert.Equal(21, request.ClipTick);
        Assert.Null(ReferenceFrameRequest.ParseArguments(["--reference-frame", "planning.rchsave", "frame.bmp"])!.ClipTick);
    }

    // FND-COMBAT-011: a later clip of the presentation is named by its index, with its tick.
    [Fact]
    public void TheClipIndexIsReadAsGiven()
    {
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--clip-tick", "4", "--clip-index", "2"])!;
        Assert.Equal(2, request.ClipIndex);
        Assert.Equal(4, request.ClipTick);
        Assert.Null(ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--clip-tick", "4"])!.ClipIndex);
    }

    // FND-UI-054, FND-COMLINK-010: the idle warning's and the Send caret's phases are separate
    // values, each read into its own field.
    [Fact]
    public void TheIdleAndCaretPhasesAreReadAsGiven()
    {
        var request = ReferenceFrameRequest.ParseArguments(
            ["--reference-frame", "planning.rchsave", "frame.bmp", "--idle-phase", "7", "--caret-phase", "5"])!;
        Assert.Equal(7, request.IdlePhase);
        Assert.Equal(5, request.CaretPhase);
        Assert.Null(request.ItemFrame);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void InvalidCaptureCannotFallBackToOrdinaryStartup(string[] args) =>
        Assert.Throws<ArgumentException>(() => ReferenceFrameRequest.ParseArguments(args));
}
