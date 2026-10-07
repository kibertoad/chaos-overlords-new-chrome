using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Screens against captures of the original. An experiment run recorded with the probe's
/// <c>--capture</c> holds a capture of the drawing area at its endpoint, with the xxh3 and the
/// count of exact white pixels of each screen element it is compared at. The rebuild replays the
/// run, draws its endpoint with <c>--reference-frame</c> at the capture's marker frame, and has to
/// draw every element as the original did, outside the areas a deviation draws
/// (<see cref="ScreenCaptureMasks"/>). docs/VALIDATION.md gives the workflow.
/// </summary>
public sealed partial class ScreenCaptureTests
{
    public static TheoryData<string, int, int> Captures()
    {
        var data = new TheoryData<string, int, int>();
        foreach (var capture in ScreenCaptureRecord.LoadAll()) data.Add(capture.Experiment, capture.Run, capture.Step);
        return data;
    }

    private static ScreenCaptureRecord Capture(string experiment, int run, int step) =>
        ScreenCaptureRecord.LoadAll().Single(record =>
            record.Experiment == experiment && record.Run == run && record.Step == step);

    // The captures cover SCR-UI-003 and SCR-HIRE-002 (EXP-UI-001, EXP-UI-006), SCR-UI-004 and
    // SCR-UI-005 (EXP-UI-006, EXP-UI-007), SCR-UI-007 (EXP-UI-007), SCR-UI-008 (EXP-UI-006) and
    // both variants of SCR-FINANCE-001 (EXP-UI-006, EXP-UI-007), and SCR-EVENT-001, SCR-COMBAT-001,
    // SCR-OBJECTIVE-001, SCR-SEARCH-001, SCR-HIRE-001 and SCR-GANG-002 (EXP-UI-008), and SCR-MOVE-001,
    // SCR-EQUIP-001, SCR-RESEARCH-001, SCR-UI-006 and SCR-GANG-001 (EXP-UI-009), and SCR-GIVE-001,
    // SCR-SELL-001 and SCR-INFLUENCE-001 (EXP-UI-010), SCR-ATTACK-001 (EXP-UI-011), and
    // SCR-OPTIONS-001 (EXP-UI-012), and the title screen SCR-UI-001, the credits SCR-UI-002 and the
    // setup screen SCR-SETUP-001 (EXP-UI-015). The site and Force meters of RULE-UI-005 and the
    // sector values of RULE-UI-011 are compared as elements of those screens, and the pylons of
    // RULE-UI-012 on the city map of Siege (EXP-UI-013) and Big Man (EXP-UI-014). The setup steps
    // of EXP-UI-015 compare the first setup of RULE-SETUP-002 and RULE-SETUP-010, the card presses
    // of RULE-SETUP-009, Add and Remove of RULE-SETUP-010, and setup buttons released inside and
    // outside (RULE-UI-001). The Done press of EXP-UI-012 opens the warning of RULE-OPTIONS-003
    // from the original's gangs, one of them idle. EXP-UI-016 compares the hand-off card SCR-SETUP-002
    // and the Comlink Send panel SCR-COMLINK-002 of a match of two humans. The console presses
    // before these captures route as RULE-UI-002 reads them. EXP-UI-017 compares the endgame
    // SCR-AWARDS-001 on both tabs, and EXP-UI-018 the elimination card SCR-OBJECTIVE-002 the only
    // local human sees where its planning would have come (RULE-OBJECTIVE-005). EXP-UI-019 and
    // EXP-UI-020 compare the Detailed Combat panel SCR-COMBAT-002 at the ticks of a gang's clip and
    // a police clip the console's control started, the rebuild's clip drawn at the captured tick
    // (FND-COMBAT-016). EXP-UI-021 compares Comlink View SCR-COMLINK-001 on a message one human
    // typed and sent the other, the text typed into the rebuild's Send panel. EXP-UI-023 compares
    // the victory splash SCR-AWARDS-002 of a match left with one active player.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Captures))]
    public void TheRebuildDrawsWhatTheOriginalDrew(string experiment, int run, int step)
    {
        var capture = Capture(experiment, run, step);
        if (capture.BeforeMatch is null && !OriginalNewGameExperimentTests.IsReplayed(experiment))
            Assert.Skip($"{capture} comes from a run that is not replayed, so the rebuild has no endpoint to draw.");
        if (capture.Unreplayable is { } reason)
            Assert.Skip($"{capture} cannot be reached in the rebuild: {reason}.");
        if (capture.Elements.Count == 0)
            Assert.Skip($"{capture} records no screen elements; the probe's digest command adds them.");
        // FND-COMBAT-016: the probe keeps a shot whose clip tick moved during every attempt without
        // the tick, and drawing the clip at its first tick would compare a different picture.
        if (capture.ClipTick is null && capture.Screens.Contains("SCR-COMBAT-002"))
            Assert.Skip($"{capture} shows a Detailed Combat clip, but the probe could not settle its tick.");
        var masks = ScreenCaptureMasks.For(capture.Screens);
        // The capture itself is only needed for elements with white or masked pixels; the
        // others are compared by digest.
        var path = OriginalGameFiles.ResolveCapture(
            Environment.GetEnvironmentVariable(OriginalGameFiles.EnvironmentVariable), capture.Xxh3);
        var original = path is null ? null : ScreenFrame.ReadBitmap(File.ReadAllBytes(path));
        var rebuild = capture.BeforeMatch is { } screen
            ? RebuildFrame.RenderBeforeMatch(screen,
                $"{experiment}-{run}-{screen}" + (capture.SetupStep is { } setupStep ? $"-{setupStep}" : ""),
                capture.Clicks)
            : RebuildFrame.Render(
                OriginalNewGameExperimentTests.ReplayedMatch(experiment, run), capture.MarkerFrame, capture.Clicks,
                $"{experiment}-{run}-{step}", capture.FrameCounter, capture.SelectedSector, capture.Lamps,
                capture.ItemFrame, clipTick: capture.ClipTick);

        var results = capture.Elements
            .Select(element => ScreenComparison.Compare(element, original, rebuild, masks, capture.WhiteKeyed)).ToArray();
        var output = TestContext.Current.TestOutputHelper;
        foreach (var result in results) output?.WriteLine(result.ToString());
        Assert.Empty(results.Where(result => result.Verdict == ElementVerdict.Differs).Select(result => result.ToString()));
    }

    // The comparison needs a frame that depends on nothing but the state and the marker frame:
    // two runs of the game at different marker frames differ only in the marker (FND-UI-038), at
    // (50 + 70n, 6, 20, 20) for viewed player n, so nothing else on the screen moves with time.
    [Fact]
    public void OnlyTheMarkerFrameChangesAReplayedEndpoint()
    {
        var match = OriginalNewGameExperimentTests.ReplayedMatch("EXP-SETUP-001", 0);
        var first = RebuildFrame.Render(match, 6);
        var other = RebuildFrame.Render(match, 0);
        var marker = new Rectangle(50, 6, 20, 20);
        var changed = Enumerable.Range(0, ScreenFrame.Width * ScreenFrame.Height)
            .Where(at => first.Pixels[at] != other.Pixels[at])
            .Select(at => new Point(at % ScreenFrame.Width, at / ScreenFrame.Width))
            .ToArray();
        Assert.NotEmpty(changed);
        Assert.All(changed, point => Assert.True(marker.Contains(point), $"{point} lies outside the marker."));
    }

    // Every capture names screens the masks know, and its elements lie inside the drawing area.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Captures))]
    public void EveryCaptureIsWellFormed(string experiment, int run, int step)
    {
        var capture = Capture(experiment, run, step);
        Assert.Matches("^[0-9a-f]{32}$", capture.Xxh3);
        Assert.InRange(capture.MarkerFrame, 0, 11);
        _ = ScreenCaptureMasks.For(capture.Screens);
        var area = new Rectangle(0, 0, ScreenFrame.Width, ScreenFrame.Height);
        foreach (var element in capture.Elements)
        {
            Assert.True(area.Contains(element.Rect), $"{element.Element} {element.Rect} lies outside the drawing area.");
            Assert.InRange(element.White, 0, element.Area);
        }
    }

    // The Departs from item of a deviation entry, with its indented continuation lines.
    [GeneratedRegex(@"^- Departs from: (?<from>.*(?:\n  .*)*)$", RegexOptions.Multiline)]
    private static partial Regex DeviationDeparture();

    // A mask may only hide what a deviation of that screen draws.
    [Fact]
    public void EveryMaskCitesADeviationFromItsScreen()
    {
        var departures = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "deviations"), "DEV-*.md")
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => DeviationDeparture().Match(File.ReadAllText(path).Replace("\r\n", "\n")));
        foreach (var (screen, masks) in ScreenCaptureMasks.ByScreen)
        foreach (var mask in masks)
        {
            Assert.True(departures.TryGetValue(mask.Deviation, out var departure), $"{mask.Deviation} is not in deviations/.");
            Assert.True(departure.Success, $"deviations/{mask.Deviation}.md has no Departs from item.");
            Assert.Contains(screen, departure.Groups["from"].Value);
        }
    }

    [Fact]
    public void AMatchingElementIsComparedByDigestWithoutTheCapture()
    {
        var frame = Frame((x, y) => (uint)(x * 7 + y * 13) & 0xFFFF);
        var element = Element(new Rectangle(10, 10, 20, 20), frame, out _);
        Assert.Equal(ElementVerdict.Matches, ScreenComparison.Compare(element, null, frame, []).Verdict);

        var other = Frame((x, y) => x == 15 && y == 15 ? 0x123456u : ((uint)(x * 7 + y * 13) & 0xFFFF));
        Assert.Equal(ElementVerdict.Differs, ScreenComparison.Compare(element, null, other, []).Verdict);
    }

    [Fact]
    public void AnElementTheOriginalDrewSolidWhiteIsUnverified()
    {
        var original = Frame((x, y) => x is >= 100 and < 140 && y is >= 50 and < 70 ? ScreenFrame.White : 0x202020u);
        var element = Element(new Rectangle(100, 50, 40, 20), original, out var white);
        Assert.Equal(800, white);
        var rebuild = Frame((_, _) => 0x202020u);
        var result = ScreenComparison.Compare(element, original, rebuild, []);
        Assert.Equal(ElementVerdict.Unverified, result.Verdict);
        Assert.Equal(ElementVerdict.Unverified, ScreenComparison.Compare(element, null, rebuild, []).Verdict);
        Assert.Contains("solid white", result.Note);
    }

    [Fact]
    public void WhitePixelsInsideAnElementAreCountedAsUnverified()
    {
        // A white block over part of the element: the rest is compared, the block is not.
        var original = Frame((x, y) => x < 110 && y < 60 ? ScreenFrame.White : 0x445566u);
        var element = Element(new Rectangle(100, 50, 40, 20), original, out _);
        var rebuild = Frame((_, _) => 0x445566u);
        var result = ScreenComparison.Compare(element, original, rebuild, []);
        Assert.Equal(ElementVerdict.Matches, result.Verdict);
        Assert.Equal(100, result.Unverified);

        // Without the capture the white pixels cannot be told apart, so nothing is claimed.
        Assert.Equal(ElementVerdict.Unverified, ScreenComparison.Compare(element, null, rebuild, []).Verdict);

        var wrong = Frame((x, y) => x == 130 && y == 65 ? 0u : 0x445566u);
        var differs = ScreenComparison.Compare(element, original, wrong, []);
        Assert.Equal(ElementVerdict.Differs, differs.Verdict);
        Assert.Equal(1, differs.Differing);
    }

    [Fact]
    public void WhiteInAWhiteKeyedCaptureIsCompared()
    {
        // With --white-key the keyed copies leave their white out (FND-PLATFORM-014), so the white
        // left in the capture is drawn on purpose and the rebuild has to draw it too.
        var original = Frame((x, y) => x < 110 && y < 60 ? ScreenFrame.White : 0x445566u);
        var element = Element(new Rectangle(100, 50, 40, 20), original, out _);
        var matching = ScreenComparison.Compare(element, original, original, [], whiteKeyed: true);
        Assert.Equal(ElementVerdict.Matches, matching.Verdict);
        Assert.Equal(0, matching.Unverified);
        Assert.Equal(ElementVerdict.Matches, ScreenComparison.Compare(element, null, original, [], whiteKeyed: true).Verdict);

        var rebuild = Frame((_, _) => 0x445566u);
        var differs = ScreenComparison.Compare(element, original, rebuild, [], whiteKeyed: true);
        Assert.Equal(ElementVerdict.Differs, differs.Verdict);
        Assert.Equal(100, differs.Differing);
        Assert.Equal(ElementVerdict.Differs, ScreenComparison.Compare(element, null, rebuild, [], whiteKeyed: true).Verdict);

        var solid = Element(new Rectangle(100, 50, 10, 10), original, out var white);
        Assert.Equal(100, white);
        Assert.Equal(ElementVerdict.Differs, ScreenComparison.Compare(solid, original, rebuild, [], whiteKeyed: true).Verdict);
    }

    [Fact]
    public void AFixtureWithTheKeyColourInputIsWhiteKeyed()
    {
        using var keyed = JsonDocument.Parse("""
            {"inputs":[{"tick":0,"name":"setup","value":"scenario 0"},
                       {"tick":0,"name":"setup","value":"key_colour RGB(255,255,255)"}]}
            """);
        using var plain = JsonDocument.Parse("""{"inputs":[{"tick":0,"name":"setup","value":"scenario 0"}]}""");
        Assert.True(ScreenCaptureRecord.IsWhiteKeyed(keyed.RootElement));
        Assert.False(ScreenCaptureRecord.IsWhiteKeyed(plain.RootElement));
        Assert.Contains(ScreenCaptureRecord.LoadAll(), capture => capture.Experiment == "EXP-UI-003" && capture.WhiteKeyed);
        Assert.Contains(ScreenCaptureRecord.LoadAll(), capture => capture.Experiment == "EXP-UI-001" && !capture.WhiteKeyed);
    }

    [Fact]
    public void AMaskedAreaIsLeftOutAndNamesItsDeviation()
    {
        var original = Frame((_, _) => 0x101010u);
        var element = Element(new Rectangle(0, 430, 200, 20), original, out _);
        var rebuild = Frame((x, y) => y is >= 439 and < 446 && x is >= 18 and < 100 ? 0xB4BEBEu : 0x101010u);
        var masks = new[] { new CaptureMask("DEV-UI-023", new Rectangle(2, 439, 432, 7)) };
        var result = ScreenComparison.Compare(element, original, rebuild, masks);
        Assert.Equal(ElementVerdict.Matches, result.Verdict);
        Assert.Equal(198 * 7, result.Masked);
        Assert.Contains("DEV-UI-023", result.Note);
        Assert.Equal(ElementVerdict.Differs, ScreenComparison.Compare(element, original, rebuild, []).Verdict);
    }

    [Fact]
    public void ACaptureThatDoesNotMatchItsFixtureIsRefused()
    {
        var original = Frame((_, _) => 0x101010u);
        var element = Element(new Rectangle(0, 0, 10, 10), original, out _) with { Xxh3 = new string('0', 32) };
        Assert.Throws<InvalidDataException>(() => ScreenComparison.Compare(element, original, original, []));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BitmapsAreReadInEitherRowOrder(bool topDown)
    {
        var frame = Frame((x, y) => (uint)(x << 8 | y) & 0xFFFFFF);
        Assert.Equal(frame.Pixels, ScreenFrame.ReadBitmap(Bitmap(frame, topDown)).Pixels);
    }

    [Fact]
    public void AFixtureCaptureIsParsed()
    {
        using var json = JsonDocument.Parse("""
            {"xxh3":"00112233445566778899aabbccddeeff","area":[0,0,640,460],"marker_frame":6,
             "screens":[{"screen":"SCR-HIRE-002","elements":[
               {"element":"Offer portrait, slot 0","rect":[440,373,64,64],"xxh3":"ffeeddccbbaa99887766554433221100","white":3}]}]}
            """);
        var capture = ScreenCaptureRecord.Parse("EXP-SETUP-001", 0, json.RootElement);
        Assert.Equal(6, capture.MarkerFrame);
        Assert.Equal(["SCR-HIRE-002"], capture.Screens);
        var element = Assert.Single(capture.Elements);
        Assert.Equal(new Rectangle(440, 373, 64, 64), element.Rect);
        Assert.Equal(3, element.White);
    }

    private static ScreenFrame Frame(Func<int, int, uint> pixel)
    {
        var pixels = new uint[ScreenFrame.Width * ScreenFrame.Height];
        for (var y = 0; y < ScreenFrame.Height; y++)
        for (var x = 0; x < ScreenFrame.Width; x++)
            pixels[y * ScreenFrame.Width + x] = pixel(x, y);
        return new ScreenFrame(pixels);
    }

    // The element a fixture would hold for this rectangle of the original.
    private static CapturedElement Element(Rectangle rect, ScreenFrame original, out int white)
    {
        white = 0;
        for (var y = rect.Top; y < rect.Bottom; y++)
        for (var x = rect.Left; x < rect.Right; x++)
            if (original[x, y] == ScreenFrame.White) white++;
        return new CapturedElement("SCR-UI-003", "test element", rect, original.Digest(rect), white);
    }

    // A 32-bit bitmap as the probe and the rebuild write them, or stored bottom row first.
    private static byte[] Bitmap(ScreenFrame frame, bool topDown)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var size = ScreenFrame.Width * ScreenFrame.Height * 4;
        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(54 + size); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(ScreenFrame.Width); writer.Write(topDown ? -ScreenFrame.Height : ScreenFrame.Height);
        writer.Write((short)1); writer.Write((short)32); writer.Write(0);
        writer.Write(size); writer.Write(0L); writer.Write(0L);
        for (var row = 0; row < ScreenFrame.Height; row++)
        {
            var y = topDown ? row : ScreenFrame.Height - 1 - row;
            for (var x = 0; x < ScreenFrame.Width; x++) writer.Write(frame[x, y]);
        }
        writer.Flush();
        return stream.ToArray();
    }
}
