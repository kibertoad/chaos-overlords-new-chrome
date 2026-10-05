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
        new[] { "--selected-sector", "1" },
        new[] { "--reference-frame", "save", "frame", "--selected-sector", "64" },
        new[] { "--reference-frame", "save", "frame", "--selected-sector", "-1" },
        new[] { "--item-frame", "1" },
        new[] { "--reference-frame", "save", "frame", "--item-frame", "15" },
        new[] { "--reference-frame", "save", "frame", "--item-frame", "1", "--item-frame", "2" },
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
    };

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

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void InvalidCaptureCannotFallBackToOrdinaryStartup(string[] args) =>
        Assert.Throws<ArgumentException>(() => ReferenceFrameRequest.ParseArguments(args));
}
