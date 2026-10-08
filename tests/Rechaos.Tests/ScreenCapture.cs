using System.Diagnostics;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Rechaos.Core;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// A 640-by-460 frame as 0x00RRGGBB values from the top row down: a capture of the original taken
/// with the probe's <c>new-game --capture</c>, or a frame the rebuild drew with
/// <c>--reference-frame</c>. Both write 32-bit bitmaps.
/// </summary>
public sealed class ScreenFrame
{
    public const int Width = 640;
    public const int Height = 460;
    public const uint White = 0xFFFFFF;

    public ScreenFrame(uint[] pixels)
    {
        if (pixels.Length != Width * Height)
            throw new ArgumentException($"A frame holds {Width * Height} pixels, not {pixels.Length}.", nameof(pixels));
        Pixels = pixels;
    }

    public uint[] Pixels { get; }

    public uint this[int x, int y] => Pixels[y * Width + x];

    public static ScreenFrame ReadBitmap(byte[] bytes)
    {
        var offset = BitConverter.ToInt32(bytes, 10);
        var width = BitConverter.ToInt32(bytes, 18);
        var height = BitConverter.ToInt32(bytes, 22);
        var depth = BitConverter.ToInt16(bytes, 28);
        if (bytes[0] != 'B' || bytes[1] != 'M' || width != Width || Math.Abs(height) != Height || depth != 32)
            throw new InvalidDataException($"Not a {Width}-by-{Height} 32-bit bitmap ({width} by {height} at {depth} bits).");
        var pixels = new uint[Width * Height];
        for (var row = 0; row < Height; row++)
        {
            // A negative height stores the top row first.
            var y = height < 0 ? row : Height - 1 - row;
            for (var x = 0; x < Width; x++)
                pixels[y * Width + x] = BitConverter.ToUInt32(bytes, offset + (row * Width + x) * 4) & White;
        }
        return new ScreenFrame(pixels);
    }

    /// <summary>
    /// The digest of a rectangle, as the probe's <c>extract</c> writes it into a fixture: the xxh3
    /// of its pixels as red, green and blue bytes, row by row from the top and left to right.
    /// </summary>
    public string Digest(Rectangle rect)
    {
        var rgb = new byte[rect.Width * rect.Height * 3];
        var at = 0;
        for (var y = rect.Top; y < rect.Bottom; y++)
        for (var x = rect.Left; x < rect.Right; x++)
        {
            var pixel = this[x, y];
            rgb[at++] = (byte)(pixel >> 16);
            rgb[at++] = (byte)(pixel >> 8);
            rgb[at++] = (byte)pixel;
        }
        return SpecHash.Xxh3(rgb);
    }
}

/// <summary>One element of a screen entry at the rectangle a capture recorded for it.</summary>
public sealed record CapturedElement(string Screen, string Element, Rectangle Rect, string Xxh3, int White)
{
    public int Area => Rect.Width * Rect.Height;
}

/// <summary>
/// A capture of the original recorded in an experiment fixture: the run whose endpoint it shows,
/// or the order step after which a <c>shot</c> step took it (-1 for the endpoint), the xxh3 of the
/// bitmap kept under <c>GAME_DIR/captures/</c>, the Overlord bar's marker frame (FND-UI-038), the
/// elements it is compared at, and the clicks that take the rebuild from the endpoint to the same
/// screen. <see cref="Unreplayable"/> says why no clicks can, when that is so.
/// <see cref="WhiteKeyed"/> is set when the fixture lists the setup input <c>key_colour</c>: the
/// probe's <c>--white-key</c> gave the original's keyed copies the white a 32-bit surface holds
/// (FND-PLATFORM-014), so the capture's exact white is the white the original means to draw.
/// </summary>
public sealed record ScreenCaptureRecord(
    string Experiment, int Run, string Xxh3, int MarkerFrame, IReadOnlyList<string> Screens,
    IReadOnlyList<CapturedElement> Elements, bool WhiteKeyed = false, int Step = -1,
    IReadOnlyList<ReferenceClick>? Clicks = null, string? Unreplayable = null, int? PumpCounter = null,
    int? SelectedSector = null, ReferenceLamps? Lamps = null)
{
    private const string KeyColourInput = "key_colour ";

    /// <summary>
    /// FND-UI-048, FND-UI-051: the counter whose selection frame the capture shows. A shot records
    /// it as <c>frame_counter</c>, null when a panel stopped the frame before the probe watched; an
    /// older capture records only the pump's counter.
    /// </summary>
    public int? FrameCounter { get; init; } = PumpCounter;

    /// <summary>FND-UI-052, FND-UI-053: the frame of the rotating item pictures a shot shows.</summary>
    public int? ItemFrame { get; init; }

    /// <summary>FND-UI-054: the idle gang warning's ticks since its open, modulo 8, a shot shows.</summary>
    public int? IdlePhase { get; init; }

    /// <summary>FND-COMLINK-010: the Comlink Send caret's phase a shot shows, 3 inverse and 0 plain.</summary>
    public int? CaretPhase { get; init; }

    /// <summary>FND-COMBAT-016: the tick of the Detailed Combat clip a shot shows.</summary>
    public int? ClipTick { get; init; }

    /// <summary>
    /// FND-COMBAT-011: the index within its presentation of the Detailed Combat clip a shot shows.
    /// A shot recorded before the probe kept it has none; those shots all show a first clip.
    /// </summary>
    public int? ClipIndex { get; init; }

    /// <summary>
    /// The screens a run copies before its match (FND-UI-055): the fixture holds each as
    /// <c>&lt;screen&gt;_capture</c>, and the rebuild draws it with that name in place of a save.
    /// Each has a <see cref="Step"/> of its own below -1.
    /// </summary>
    public static readonly IReadOnlyList<(string Screen, int Step)> BeforeMatchScreens =
        [("title", -2), ("credits", -3), ("setup", -4)];

    /// <summary>The screen shown before a match the capture shows, or null.</summary>
    public string? BeforeMatch { get; init; }

    /// <summary>The setup step after which <c>--setup-steps</c> took the copy, or null.</summary>
    public int? SetupStep => BeforeMatch is not null && Step <= SetupStepBase ? SetupStepBase - Step : null;

    public override string ToString() => BeforeMatch is { } screen
        ? $"{Experiment} run {Run} {screen}" + (SetupStep is { } setupStep ? $" step {setupStep}" : "")
        : Step < 0 ? $"{Experiment} run {Run}" : $"{Experiment} run {Run} step {Step}";

    // The fixtures run to tens of megabytes, and every theory case looks its capture up here.
    private static readonly Lazy<IReadOnlyList<ScreenCaptureRecord>> All = new(Load);

    /// <summary>Every capture the experiment fixtures beside the test binary record.</summary>
    public static IReadOnlyList<ScreenCaptureRecord> LoadAll() => All.Value;

    private static IReadOnlyList<ScreenCaptureRecord> Load()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "spec", "experiments");
        var records = new List<ScreenCaptureRecord>();
        foreach (var file in Directory.EnumerateFiles(directory, "EXP-*.json").Order(StringComparer.Ordinal))
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(file));
            var experiment = fixture.RootElement.GetProperty("experiment").GetString()!;
            var whiteKeyed = IsWhiteKeyed(fixture.RootElement);
            var run = 0;
            foreach (var recorded in fixture.RootElement.GetProperty("runs").EnumerateArray())
            {
                if (recorded.TryGetProperty("capture", out var capture))
                    records.Add(Parse(experiment, run, capture, whiteKeyed));
                // --white-key patches the key before the game runs, so the screens copied before
                // the match are keyed as the match's are.
                foreach (var (screen, step) in BeforeMatchScreens)
                    if (recorded.TryGetProperty(screen + "_capture", out var before))
                        records.Add(Parse(experiment, run, before, whiteKeyed) with { Step = step, BeforeMatch = screen });
                if (recorded.TryGetProperty("setup_steps", out var setupSteps))
                    records.AddRange(SetupStepCaptures(experiment, run, setupSteps.EnumerateArray().ToArray(), whiteKeyed));
                if (recorded.TryGetProperty("order_steps", out var steps))
                    records.AddRange(StepCaptures(experiment, run, steps.EnumerateArray().ToArray(), whiteKeyed));
                run++;
            }
        }
        return records;
    }

    /// <summary>
    /// The first step number of the copies <c>--setup-steps</c> took on the setup screen; the copy
    /// after setup step <c>n</c> is step <c>SetupStepBase - n</c>.
    /// </summary>
    public const int SetupStepBase = -100;

    // Each setup step's copy is drawn by the rebuild's setup screen after the presses before it.
    private static IEnumerable<ScreenCaptureRecord> SetupStepCaptures(string experiment, int run, JsonElement[] steps, bool whiteKeyed)
    {
        var clicks = new List<ReferenceClick>();
        var named = false;
        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            if (named && step.TryGetProperty("capture", out _))
                throw new InvalidDataException(
                    $"{experiment} run {run} copies the setup screen after a name step, whose keys the rebuild does not replay.");
            var point = new Point(step.GetProperty("x").GetInt32(), step.GetProperty("y").GetInt32());
            if (step.GetProperty("kind").GetString() == "strip")
                clicks.Add(new ReferenceClick(point));
            else if (step.GetProperty("kind").GetString() == "drag")
                clicks.Add(new ReferenceClick(point,
                    Release: new Point(step.GetProperty("to_x").GetInt32(), step.GetProperty("to_y").GetInt32())));
            else if (step.GetProperty("kind").GetString() == "name")
            {
                // The press on the name band opens the editor; the copy is taken with it open,
                // before any key, so the keys a step types after it are not replayed. A copy after
                // a name step that typed would need them.
                clicks.Add(new ReferenceClick(point));
                if (step.TryGetProperty("capture", out var underDialog))
                    yield return Parse(experiment, run, underDialog, whiteKeyed) with
                    {
                        Step = SetupStepBase - index, BeforeMatch = "setup", Clicks = clicks.ToArray(),
                    };
                named = true;
            }
            else if (step.TryGetProperty("capture", out var capture))
                yield return Parse(experiment, run, capture, whiteKeyed) with
                {
                    Step = SetupStepBase - index, BeforeMatch = "setup", Clicks = clicks.ToArray(),
                };
        }
    }

    /// <summary>Whether a fixture's inputs hold the <c>key_colour</c> setup input of <c>--white-key</c>.</summary>
    public static bool IsWhiteKeyed(JsonElement fixture) =>
        fixture.TryGetProperty("inputs", out var inputs)
        && inputs.EnumerateArray().Any(input =>
            input.GetProperty("name").GetString() == "setup"
            && input.GetProperty("value").GetString()!.StartsWith(KeyColourInput, StringComparison.Ordinal));

    public static ScreenCaptureRecord Parse(string experiment, int run, JsonElement capture, bool whiteKeyed = false)
    {
        var screens = new List<string>();
        var elements = new List<CapturedElement>();
        // A capture recorded before element digests were taken has no screens until the probe's
        // digest command adds them.
        var recorded = capture.TryGetProperty("screens", out var list) ? list.EnumerateArray().ToArray() : [];
        foreach (var screen in recorded)
        {
            var id = screen.GetProperty("screen").GetString()!;
            screens.Add(id);
            foreach (var element in screen.GetProperty("elements").EnumerateArray())
            {
                var rect = element.GetProperty("rect").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                elements.Add(new CapturedElement(id, element.GetProperty("element").GetString()!,
                    new Rectangle(rect[0], rect[1], rect[2], rect[3]),
                    element.GetProperty("xxh3").GetString()!, element.GetProperty("white").GetInt32()));
            }
        }
        var record = new ScreenCaptureRecord(experiment, run, capture.GetProperty("xxh3").GetString()!,
            capture.GetProperty("marker_frame").GetInt32(), screens, elements, whiteKeyed,
            PumpCounter: capture.TryGetProperty("pump_counter", out var pump) && pump.ValueKind == JsonValueKind.Number
                ? pump.GetInt32()
                : null,
            SelectedSector: capture.TryGetProperty("selected_sector", out var selected)
                            && selected.ValueKind == JsonValueKind.Number
                ? selected.GetInt32()
                : null,
            // FND-EVENT-006: each light's flag, then the byte that says its lamp is drawn lit.
            Lamps: capture.TryGetProperty("lamps", out var lamps) && lamps.ValueKind == JsonValueKind.Array
                && lamps.EnumerateArray().Select(value => value.GetInt32()).ToArray() is [_, var events, _, var comlink]
                ? new ReferenceLamps(events != 0, comlink != 0)
                : null)
        {
            ItemFrame = capture.TryGetProperty("item_frame", out var item) && item.ValueKind == JsonValueKind.Number
                ? item.GetInt32()
                : null,
            IdlePhase = capture.TryGetProperty("idle_phase", out var idle) && idle.ValueKind == JsonValueKind.Number
                ? idle.GetInt32()
                : null,
            CaretPhase = capture.TryGetProperty("caret_phase", out var caret) && caret.ValueKind == JsonValueKind.Number
                ? caret.GetInt32()
                : null,
            ClipTick = capture.TryGetProperty("clip_tick", out var tick) && tick.ValueKind == JsonValueKind.Number
                ? tick.GetInt32()
                : null,
            ClipIndex = capture.TryGetProperty("clip_index", out var clip) && clip.ValueKind == JsonValueKind.Number
                ? clip.GetInt32()
                : null,
        };
        // Without frame_counter the record keeps the pump's counter as its frame counter.
        return capture.TryGetProperty("frame_counter", out var frame)
            ? record with { FrameCounter = frame.ValueKind == JsonValueKind.Number ? frame.GetInt32() : null }
            : record;
    }

    // The captures shot steps of --order-steps took after the dump. The rebuild reaches each one's
    // screen from the endpoint by the same presses: a double-click at the centre of the opened
    // sector's cell (FND-UI-015), a press at a card's or the window's point, a double-click at a
    // window's point, and the back control.
    // Its reference frame never opens the result panels the planning entry would open first, so
    // the presses of their Exit are left out. The rebuild's orders are a panel (DEV-UI-021): a
    // card's order menu (menu 1) whose choice opens a picker (FND-UI-021) is replayed as the card
    // press and a press on that order's row of the panel, which opens the same picker. Any other
    // popup menu has no counterpart.
    // FND-UI-021: menu 1 lists the one-off orders, in the rows of the rebuild's panel.
    private const int OrderMenu = 1;

    // The row of the rebuild's order panel for a menu 1 command that runs a picker, or null.
    private static int? PickerRow(int command)
    {
        var actions = OriginalNewGameExperimentTests.MenuActions(OrderMenu);
        for (var row = 0; row < actions.Count; row++)
            if (actions[row].Command == command)
                return CommandOverlayLayout.OpensTargetPicker(actions[row].Action) ? row : null;
        return null;
    }

    private static IEnumerable<ScreenCaptureRecord> StepCaptures(
        string experiment, int run, JsonElement[] steps, bool whiteKeyed)
    {
        var clicks = new List<ReferenceClick>();
        string? unreplayable = null;
        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            int Number(string name) => step.GetProperty(name).GetInt32();
            var menu = step.GetProperty("menu").GetInt32();
            var pickerRow = menu == OrderMenu && step.GetProperty("kind").GetString() == "card"
                ? PickerRow(Number("choice"))
                : null;
            if (menu > 0 && pickerRow is null)
                unreplayable ??= $"step {index} opened popup menu {step.GetProperty("menu").GetInt32()}, which the rebuild draws as a panel (DEV-UI-021)";
            // EXP-UI-043: the original presses a console tile with the right button and leaves
            // the sector view at a right press on Back; the replay has no right presses until #525
            // compares these shots.
            if (step.GetProperty("kind").GetString() is "rdown" or "rup")
                unreplayable ??= $"step {index} presses the right button, which the replay does not press yet (#525)";
            switch (step.GetProperty("kind").GetString())
            {
                case "open":
                    var sector = Number("target");
                    clicks.Add(new ReferenceClick(
                        new Point(2 + 54 * (sector % 8) + 27, 42 + 52 * (sector / 8) + 26), Double: true));
                    break;
                case "card":
                    var card = Number("target");
                    clicks.Add(new ReferenceClick(new Point(
                        SectorGangCardLayout.Left + card % 2 * SectorGangCardLayout.ColumnStride + Number("x"),
                        SectorGangCardLayout.Top + card / 2 * SectorGangCardLayout.RowStride + Number("y"))));
                    if (pickerRow is { } row)
                        clicks.Add(new ReferenceClick(CommandOverlayLayout.ActionRow(row).Center));
                    break;
                case "strip":
                    clicks.Add(new ReferenceClick(new Point(Number("x"), Number("y"))));
                    break;
                case "dbl":
                    clicks.Add(new ReferenceClick(new Point(Number("x"), Number("y")), Double: true));
                    break;
                case "back":
                    clicks.Add(new ReferenceClick(SectorDetailLayout.Back.Center));
                    break;
                // A button pressed and kept down, the pointer moved with it, and its release, so a
                // shot between them shows the held control.
                case "down" or "move" or "up" or "rdown" or "rup" when step.GetProperty("kind").GetString() is { } edge:
                    clicks.Add(new ReferenceClick(new Point(Number("x"), Number("y")))
                    {
                        Edge = edge switch
                        {
                            "down" => ReferenceButtonEdge.Down,
                            "move" => ReferenceButtonEdge.Move,
                            "up" => ReferenceButtonEdge.Up,
                            "rdown" => ReferenceButtonEdge.RightDown,
                            _ => ReferenceButtonEdge.RightUp,
                        },
                    });
                    break;
                case "type":
                    clicks.Add(new ReferenceClick(Point.Zero)
                    {
                        Text = step.GetProperty("text").GetString()
                               ?? throw new InvalidDataException($"{experiment} step {index} types no text."),
                    });
                    break;
                case "shot" when step.TryGetProperty("capture", out var capture):
                    yield return Parse(experiment, run, capture, whiteKeyed) with
                    {
                        Step = index, Clicks = clicks.ToArray(), Unreplayable = unreplayable,
                    };
                    break;
            }
        }
    }
}

/// <summary>An area of a screen the rebuild draws differently on purpose, under a deviation.</summary>
public sealed record CaptureMask(string Deviation, Rectangle Rect);

/// <summary>
/// The areas each screen's comparison leaves out, each under the deviation that draws it. A mask
/// covers only what the rebuild adds or replaces; everything else on the screen is compared.
/// </summary>
public static class ScreenCaptureMasks
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<CaptureMask>> ByScreen =
        new Dictionary<string, IReadOnlyList<CaptureMask>>
        {
            ["SCR-UI-003"] =
            [
                // DEV-UI-006: the cash row shows the unspent cash and the change next to the cash.
                new("DEV-UI-006", StatusConsoleLayout.Cash),
                // DEV-UI-023: the key line along the bottom of the city map, one 7-pixel text row.
                new("DEV-UI-023", new Rectangle(2, 439, 432, 7)),
                // DEV-UI-007 turns Tolerance orange only while the queued Chaos can set off a
                // Crackdown, which no city capture shows, so Tolerance is compared here.
            ],
            ["SCR-HIRE-002"] = [],
            // DEV-FINANCE-001 changes the Equipment field only while a Sell of several items is queued.
            ["SCR-FINANCE-001"] = [],
            ["SCR-UI-004"] =
            [
                // DEV-UI-006: the console's cash row is the city screen's.
                new("DEV-UI-006", StatusConsoleLayout.Cash),
                // DEV-UI-007: the Tolerance value turns orange when the queued Chaos can set off a
                // Crackdown.
                new("DEV-UI-007", new Rectangle(StatusConsoleLayout.SectorValueLeft, StatusConsoleLayout.SectorValueY(2),
                    16, 7)),
            ],
            ["SCR-UI-005"] = [],
            ["SCR-UI-007"] = [],
            ["SCR-UI-008"] = [],
            ["SCR-EVENT-001"] = [],
            ["SCR-COMBAT-001"] = [],
            ["SCR-COMBAT-002"] = [],
            ["SCR-OBJECTIVE-001"] = [],
            ["SCR-SEARCH-001"] = [],
            ["SCR-HIRE-001"] = [],
            ["SCR-MOVE-001"] = [],
            ["SCR-EQUIP-001"] = [],
            // The progress beside each total, right-aligned to the list's edge, up to "100/100".
            ["SCR-RESEARCH-001"] = [new CaptureMask("DEV-RESEARCH-001", new Rectangle(388, 149, 44, 144))],
            ["SCR-UI-006"] = [],
            ["SCR-GANG-001"] = [],
            ["SCR-GIVE-001"] = [],
            ["SCR-SELL-001"] = [],
            ["SCR-ATTACK-001"] = [],
            ["SCR-OPTIONS-001"] = [],
            ["SCR-SETUP-002"] = [],
            ["SCR-COMLINK-001"] = [],
            ["SCR-COMLINK-002"] = [],
            ["SCR-AWARDS-001"] = [],
            ["SCR-AWARDS-002"] = [],
            ["SCR-OBJECTIVE-002"] = [],
            ["SCR-INFLUENCE-001"] = [],
            ["SCR-GANG-002"] = [],
            ["SCR-UI-001"] =
            [
                // DEV-UI-019: the rebuild's line under the logo, its buttons, which stand in for the
                // menu bar, and its credit line. The notice box is drawn only with a message.
                new("DEV-UI-019", new Rectangle(290, 282, 60, 9)),
                new("DEV-UI-019", new Rectangle(220, 292, 200, 76)),
                new("DEV-UI-019", new Rectangle(154, 376, 164, 34)),
                new("DEV-UI-019", new Rectangle(406, 376, 80, 34)),
                new("DEV-UI-019", new Rectangle(257, 430, 126, 9)),
                // DEV-VIDEO-003: the Intro button.
                new("DEV-VIDEO-003", new Rectangle(322, 376, 80, 34)),
                // DEV-UI-012: the version, right-aligned 6 pixels from the edge, as wide as the
                // build's version string.
                new("DEV-UI-012", new Rectangle(
                    VirtualInput.Width - 6 - GameVersion.Display.Length * OriginalFontLayout.CellWidth, 430,
                    GameVersion.Display.Length * OriginalFontLayout.CellWidth, 9)),
            ],
            ["SCR-UI-002"] = [],
            ["SCR-SETUP-001"] = [],
            // DEV-SETUP-003: while the name dialog is open the rebuild edits the name on card 0's
            // name row, where the original still shows the name. The dialog itself is drawn by
            // Windows, and the copy under it leaves it out.
            ["SCR-SETUP-003"] = [new CaptureMask("DEV-SETUP-003", SetupPlayerCardArtLayout.NameRow(0))],
        };

    /// <summary>The masks of every screen a capture shows, since one frame draws them all.</summary>
    public static IReadOnlyList<CaptureMask> For(IEnumerable<string> screens) =>
        screens.SelectMany(screen => ByScreen.TryGetValue(screen, out var masks)
            ? masks
            : throw new InvalidOperationException(
                $"{screen} has no entry in ScreenCaptureMasks; add one, empty when no deviation draws on it.")).ToArray();
}

public enum ElementVerdict
{
    /// <summary>Every compared pixel is the original's.</summary>
    Matches,
    /// <summary>Some compared pixel differs from the original's.</summary>
    Differs,
    /// <summary>Nothing in the rectangle could be compared.</summary>
    Unverified,
}

/// <summary>
/// The comparison of one element. <see cref="Unverified"/> counts the pixels that were not
/// compared because the original drew them exact white where the rebuild does not, and
/// <see cref="Masked"/> the pixels a deviation covers.
/// </summary>
public sealed record ElementComparison(
    CapturedElement Element, ElementVerdict Verdict, int Differing, int Unverified, int Masked, string Note)
{
    public override string ToString() =>
        $"{Element.Screen} {Element.Element} {Element.Rect}: {Verdict}"
        + (Differing > 0 ? $", {Differing} differing" : "")
        + (Unverified > 0 ? $", {Unverified} unverified" : "")
        + (Masked > 0 ? $", {Masked} masked" : "")
        + (Note.Length > 0 ? $" ({Note})" : "");
}

public static class ScreenComparison
{
    /// <summary>
    /// Compares an element's rectangle in the rebuild's frame with the original. With the capture
    /// at hand every pixel outside the masks is compared, and a pixel the original drew exact
    /// white is counted as unverified unless the rebuild drew it white too: on Windows 11 the
    /// original leaves solid white rectangles where a blit failed (FND-UI-041), and what belongs
    /// there is unknown. An element the original drew wholly white is unverified. Without the
    /// capture only the digest is compared, which needs a rectangle with no white and no mask.
    /// In a <paramref name="whiteKeyed"/> capture the keyed copies left out their white
    /// (FND-PLATFORM-014), so exact white is compared like any other colour: a pixel the original
    /// drew white and the rebuild did not differs, and the digest alone suffices for an element
    /// with white pixels and no mask.
    /// </summary>
    public static ElementComparison Compare(
        CapturedElement element, ScreenFrame? original, ScreenFrame rebuild, IReadOnlyList<CaptureMask> masks,
        bool whiteKeyed = false)
    {
        var rect = element.Rect;
        var covering = masks.Where(mask => mask.Rect.Intersects(rect)).ToArray();
        var masked = 0;
        for (var y = rect.Top; y < rect.Bottom; y++)
        for (var x = rect.Left; x < rect.Right; x++)
            if (covering.Any(mask => mask.Rect.Contains(x, y))) masked++;
        var maskNote = covering.Length == 0 ? "" : "masked by " + string.Join(", ", covering.Select(mask => mask.Deviation).Distinct());

        if (element.White == element.Area && !whiteKeyed)
            return new(element, ElementVerdict.Unverified, 0, element.Area, masked,
                Join("the original drew it solid white", maskNote));

        if (original is null)
        {
            if ((element.White > 0 && !whiteKeyed) || masked > 0)
                return new(element, ElementVerdict.Unverified, 0, element.Area - masked, masked,
                    Join($"the capture is needed: {element.White} white pixels", maskNote));
            return rebuild.Digest(rect) == element.Xxh3
                ? new(element, ElementVerdict.Matches, 0, 0, 0, "digest")
                : new(element, ElementVerdict.Differs, -1, 0, 0, "digest differs");
        }

        var digest = original.Digest(rect);
        if (digest != element.Xxh3)
            throw new InvalidDataException(
                $"{element.Screen} {element.Element}: the capture's rectangle has xxh3 {digest}, the fixture {element.Xxh3}.");

        int differing = 0, unverified = 0;
        for (var y = rect.Top; y < rect.Bottom; y++)
        for (var x = rect.Left; x < rect.Right; x++)
        {
            if (covering.Any(mask => mask.Rect.Contains(x, y))) continue;
            var theirs = original[x, y];
            var ours = rebuild[x, y];
            if (theirs == ours) continue;
            if (theirs == ScreenFrame.White && !whiteKeyed) unverified++;
            else differing++;
        }
        var verdict = differing > 0 ? ElementVerdict.Differs
            : unverified == element.Area - masked ? ElementVerdict.Unverified
            : ElementVerdict.Matches;
        return new(element, verdict, differing, unverified, masked, maskNote);
    }

    private static string Join(string first, string second) => second.Length == 0 ? first : $"{first}; {second}";
}

/// <summary>
/// Draws a match state in the rebuild: writes it as a native save and runs the game with
/// <c>--reference-frame</c>, which shows the save at its planning entry, writes the drawing area
/// and exits. Skips the test when no asset pack is installed beside the test binary or in the
/// player's application data, since the frame is drawn from the original's art.
/// </summary>
public static class RebuildFrame
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Names a directory the rebuild's frames are copied to, as <c>&lt;name&gt;.bmp</c>, for comparing
    /// them with the captures by eye.
    /// </summary>
    public const string KeepFramesVariable = "RECHAOS_KEEP_FRAMES";

    /// <summary>
    /// Draws a screen the rebuild shows before a match, one of
    /// <see cref="ReferenceFrameRequest.ScreenOperands"/>.
    /// </summary>
    public static ScreenFrame RenderBeforeMatch(
        string screen, string? name = null, IReadOnlyList<ReferenceClick>? clicks = null) =>
        Render(null, null, clicks, name, screen: screen);

    /// <summary>Why no frame can be drawn on this machine, or null when an asset pack is installed.</summary>
    public static string? MissingAssetPack() =>
        File.Exists(Path.Combine(AssetRoot(), "manifest.json")) ? null : $"No asset pack is installed at {AssetRoot()}.";

    private static string AssetRoot() => AssetRootResolver.Resolve(AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static ScreenFrame Render(
        MatchState? state, int? markerFrame, IReadOnlyList<ReferenceClick>? clicks = null, string? name = null,
        int? pumpCounter = null, int? selectedSector = null, ReferenceLamps? lamps = null, int? itemFrame = null,
        string? screen = null, int? clipTick = null, int? idlePhase = null, int? caretPhase = null, int? clipIndex = null)
    {
        if (MissingAssetPack() is { } missing) Assert.Skip(missing);
        var assets = AssetRoot();

        var directory = Path.Combine(Path.GetTempPath(), "rechaos-screen-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var save = Path.Combine(directory, "state.rchsave");
            var frame = Path.Combine(directory, "frame.bmp");
            if (state is null) save = screen ?? throw new ArgumentNullException(nameof(state));
            else NativeSaveStore.SaveAtomic(save, state);
            var start = GameStartInfo();
            foreach (var argument in new[] { "--assets", assets, "--reference-frame", save, frame })
                start.ArgumentList.Add(argument);
            if (markerFrame is { } marker)
            {
                start.ArgumentList.Add("--marker-frame");
                start.ArgumentList.Add(marker.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (selectedSector is { } sector)
            {
                start.ArgumentList.Add("--selected-sector");
                start.ArgumentList.Add(sector.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (itemFrame is { } item)
            {
                start.ArgumentList.Add("--item-frame");
                start.ArgumentList.Add(item.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (idlePhase is { } idle)
            {
                start.ArgumentList.Add("--idle-phase");
                start.ArgumentList.Add(idle.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (caretPhase is { } caret)
            {
                start.ArgumentList.Add("--caret-phase");
                start.ArgumentList.Add(caret.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (clipTick is { } tick)
            {
                start.ArgumentList.Add("--clip-tick");
                start.ArgumentList.Add(tick.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (clipIndex is { } clip)
            {
                start.ArgumentList.Add("--clip-index");
                start.ArgumentList.Add(clip.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (pumpCounter is { } counter)
            {
                start.ArgumentList.Add("--pump-counter");
                start.ArgumentList.Add(counter.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (lamps is not null)
            {
                start.ArgumentList.Add("--lamps");
                start.ArgumentList.Add(lamps.ToString());
            }
            if (clicks is { Count: > 0 })
            {
                start.ArgumentList.Add("--reference-clicks");
                start.ArgumentList.Add(string.Join(",", clicks));
            }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("The game did not start.");
            var error = new System.Text.StringBuilder();
            process.ErrorDataReceived += (_, line) => { lock (error) error.AppendLine(line.Data); };
            process.BeginErrorReadLine();
            // A worker can still be drawing a frame no row asked for when the test host exits.
            using var stop = RowPrefetchWorkers.Stopping.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            });
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"The game did not write the frame within {Timeout.TotalSeconds} seconds.");
            }
            // Waits for the redirected error stream to drain as well.
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(frame))
                throw new InvalidOperationException($"The game exited with {process.ExitCode} and no frame. {error}");
            if (name is not null && Environment.GetEnvironmentVariable(KeepFramesVariable) is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                File.Copy(frame, Path.Combine(keep, name + ".bmp"), overwrite: true);
            }
            return ScreenFrame.ReadBitmap(File.ReadAllBytes(frame));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // The game's apphost beside the test binary, or the shared host with its assembly.
    private static ProcessStartInfo GameStartInfo()
    {
        var host = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Rechaos.Game.exe" : "Rechaos.Game");
        var start = File.Exists(host)
            ? new ProcessStartInfo(host)
            : new ProcessStartInfo("dotnet") { ArgumentList = { Path.Combine(AppContext.BaseDirectory, "Rechaos.Game.dll") } };
        start.UseShellExecute = false;
        start.RedirectStandardError = true;
        start.WorkingDirectory = AppContext.BaseDirectory;
        return start;
    }
}
