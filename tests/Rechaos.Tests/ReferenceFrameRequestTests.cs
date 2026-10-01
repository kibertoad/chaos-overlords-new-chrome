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
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "rechaos-reference-frame-"), request.UserDataDirectory);
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
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void InvalidCaptureCannotFallBackToOrdinaryStartup(string[] args) =>
        Assert.Throws<ArgumentException>(() => ReferenceFrameRequest.ParseArguments(args));
}
