using System.IO.Hashing;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rechaos.OriginalProbe;

/// <summary>An element of a screen entry's Drawn elements table, at one rectangle of the capture.</summary>
internal sealed record CaptureElement(string Element, int[] Rect);

/// <summary>
/// The elements of one screen entry that a capture is compared at, read from
/// <c>Screens/&lt;SCR-ID&gt;.json</c> beside the probe. Each rectangle is the entry's Position for
/// the state the capture shows, with its formula worked out.
/// </summary>
internal sealed record CaptureScreen(string Screen, IReadOnlyList<CaptureElement> Elements)
{
    public static IReadOnlyList<CaptureScreen> Load(string? ids) =>
        ids is null
            ? []
            : ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(id =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Screens", $"{id}.json");
                var screen = JsonSerializer.Deserialize<CaptureScreen>(File.ReadAllText(path),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidDataException($"{path} holds no screen.");
                if (screen.Screen != id) throw new InvalidDataException($"{path} describes {screen.Screen}, not {id}.");
                foreach (var element in screen.Elements)
                    if (element.Rect is not [>= 0, >= 0, > 0, > 0]
                        || element.Rect[0] + element.Rect[2] > CaptureFixture.Width
                        || element.Rect[1] + element.Rect[3] > CaptureFixture.Height)
                        throw new InvalidDataException($"{path}: {element.Element} lies outside the drawing area.");
                return screen;
            }).ToArray();
}

/// <summary>
/// The fixture's record of a capture taken with <c>new-game --capture</c>: the xxh3 of the bitmap,
/// the marker frame it shows, and for each element of each screen given to <c>extract
/// --screens</c> the digest of its rectangle and how many of its pixels are exact white. The
/// bitmap itself holds the game's art, so it never goes into the repository; when GAME_DIR is set
/// it is copied to <c>GAME_DIR/captures/&lt;xxh3&gt;</c>, where the tests look for it.
/// </summary>
internal static class CaptureFixture
{
    // RULE-GFX-002: the drawing area is 640 by 460 from the client area's top-left corner.
    public const int Width = 640;
    public const int Height = 460;

    public static JsonObject? Extract(string runDirectory, JsonNode trace, IReadOnlyList<CaptureScreen> screens)
    {
        // Only two agreeing, non-repainting window copies taken while the marker counter held
        // still become a reference (NewGameSession.CaptureDrawingArea).
        var marker = trace["Notes"]!.AsArray().Select(note => note!.GetValue<string>())
            .FirstOrDefault(note => note.StartsWith("marker_frame ", StringComparison.Ordinal));
        var pump = trace["Notes"]!.AsArray().Select(note => note!.GetValue<string>())
            .FirstOrDefault(note => note.StartsWith("pump_counter ", StringComparison.Ordinal));
        var lamps = trace["Notes"]!.AsArray().Select(note => note!.GetValue<string>())
            .FirstOrDefault(note => note.StartsWith("lamps ", StringComparison.Ordinal));
        var selected = trace["Notes"]!.AsArray().Select(note => note!.GetValue<string>())
            .FirstOrDefault(note => note.StartsWith("selected_sector ", StringComparison.Ordinal));
        if (marker is null) return null;
        return Extract(Path.Combine(runDirectory, "capture-blt.bmp"),
            int.Parse(marker["marker_frame ".Length..], System.Globalization.CultureInfo.InvariantCulture),
            pump is null ? null : int.Parse(pump["pump_counter ".Length..], System.Globalization.CultureInfo.InvariantCulture),
            lamps?["lamps ".Length..].Split(' ')
                .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            selected is null ? null : int.Parse(selected["selected_sector ".Length..], System.Globalization.CultureInfo.InvariantCulture),
            screens);
    }

    /// <summary>
    /// The record of a capture <c>shot</c> step of <c>--order-steps</c> took after the dump, compared
    /// at the screens the step names.
    /// </summary>
    public static JsonObject? ExtractShot(string runDirectory, JsonNode shot, string screens)
    {
        var record = Extract(Path.Combine(runDirectory, shot["File"]!.GetValue<string>()),
            shot["MarkerFrame"]!.GetValue<int>(), shot["PumpCounter"]!.GetValue<int>(),
            shot["Lamps"]?.AsArray().Select(value => value!.GetValue<int>()).ToArray(),
            shot["SelectedSector"]?.GetValue<int>(), CaptureScreen.Load(screens));
        // FND-UI-048, FND-UI-051: the counter whose selection frame the capture shows: the pump's,
        // or the one at the slide-in of the panel open over the city. A panel the probe did not
        // see slide in leaves it unknown.
        if (record is not null)
            record["frame_counter"] = shot["FrameCounter"] is JsonNode frame ? frame.GetValue<int>() : null;
        return record;
    }

    private static JsonObject? Extract(
        string capture, int marker, int? pump, int[]? lamps, int? selected, IReadOnlyList<CaptureScreen> screens)
    {
        var repeat = Path.ChangeExtension(capture, null) + "-repeat.bmp";
        if (!File.Exists(capture) || !File.Exists(repeat)) return null;
        var bytes = File.ReadAllBytes(capture);
        if (!bytes.AsSpan().SequenceEqual(File.ReadAllBytes(repeat))) return null;

        var xxh3 = Xxh3(bytes);
        var screenArray = DigestScreens(ReadBitmap(bytes), screens);

        var gameDirectory = Environment.GetEnvironmentVariable("GAME_DIR");
        if (!string.IsNullOrWhiteSpace(gameDirectory))
        {
            var captures = Path.Combine(gameDirectory, "captures");
            Directory.CreateDirectory(captures);
            var target = Path.Combine(captures, xxh3);
            if (!File.Exists(target)) File.Copy(capture, target);
            Console.WriteLine($"Capture {xxh3} is at {target}.");
        }
        else
        {
            Console.WriteLine($"GAME_DIR is not set: copy {capture} to GAME_DIR/captures/{xxh3}.");
        }

        return new JsonObject
        {
            ["xxh3"] = xxh3,
            ["area"] = new JsonArray(0, 0, Width, Height),
            ["marker_frame"] = marker,
            ["pump_counter"] = pump is null ? null : (JsonNode)pump.Value,
            // FND-EVENT-006: the Events and Comlink lights' flags and drawn bytes, in that order.
            ["lamps"] = lamps is null ? null : new JsonArray(lamps.Select(value => (JsonNode)value).ToArray()),
            // FND-SAVE-003: the selected sector `0x004ABC80`.
            ["selected_sector"] = selected is null ? null : (JsonNode)selected.Value,
            ["screens"] = screenArray,
        };
    }

    /// <summary>
    /// <c>digest</c>: adds the screens' element digests to a capture a fixture already records,
    /// from the bitmap under <c>GAME_DIR/captures/</c>, so an older capture can be compared
    /// without running the original again. Returns the number of the run's elements.
    /// </summary>
    public static int AddScreens(JsonObject run, IReadOnlyList<CaptureScreen> screens)
    {
        var capture = run["capture"] as JsonObject
            ?? throw new InvalidDataException("The run records no capture.");
        var xxh3 = capture["xxh3"]!.GetValue<string>();
        var gameDirectory = Environment.GetEnvironmentVariable("GAME_DIR");
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new InvalidOperationException("GAME_DIR is not set.");
        var path = Path.Combine(gameDirectory, "captures", xxh3);
        var bytes = File.ReadAllBytes(path);
        if (Xxh3(bytes) != xxh3) throw new InvalidDataException($"{path} does not have xxh3 {xxh3}.");
        capture["screens"] = DigestScreens(ReadBitmap(bytes), screens);
        return screens.Sum(screen => screen.Elements.Count);
    }

    private static JsonArray DigestScreens(uint[] pixels, IReadOnlyList<CaptureScreen> screens)
    {
        var screenArray = new JsonArray();
        foreach (var screen in screens)
        {
            var elements = new JsonArray();
            foreach (var element in screen.Elements)
            {
                var (digest, white) = Digest(pixels, element.Rect);
                elements.Add(new JsonObject
                {
                    ["element"] = element.Element,
                    ["rect"] = new JsonArray(element.Rect.Select(value => (JsonNode)value).ToArray()),
                    ["xxh3"] = digest,
                    ["white"] = white,
                });
            }
            screenArray.Add(new JsonObject { ["screen"] = screen.Screen, ["elements"] = elements });
        }
        return screenArray;
    }

    private static string Xxh3(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(XxHash128.Hash(bytes));

    /// <summary>
    /// The digest of a rectangle: the xxh3 of its pixels as red, green and blue bytes, row by row
    /// from the top and left to right in each row, with the count of exact white pixels. The tests
    /// compute the same digest of the rebuild's frame (ScreenCapture.Digest).
    /// </summary>
    private static (string Digest, int White) Digest(uint[] pixels, int[] rect)
    {
        var rgb = new byte[rect[2] * rect[3] * 3];
        var white = 0;
        var at = 0;
        for (var y = rect[1]; y < rect[1] + rect[3]; y++)
        for (var x = rect[0]; x < rect[0] + rect[2]; x++)
        {
            var pixel = pixels[y * Width + x];
            rgb[at++] = (byte)(pixel >> 16);
            rgb[at++] = (byte)(pixel >> 8);
            rgb[at++] = (byte)pixel;
            if ((pixel & 0xFFFFFF) == 0xFFFFFF) white++;
        }
        return (Xxh3(rgb), white);
    }

    // The 32-bit bitmap CaptureDrawingArea writes, as 0x00RRGGBB values from the top row down.
    private static uint[] ReadBitmap(byte[] bytes)
    {
        var offset = BitConverter.ToInt32(bytes, 10);
        var width = BitConverter.ToInt32(bytes, 18);
        var height = BitConverter.ToInt32(bytes, 22);
        var depth = BitConverter.ToInt16(bytes, 28);
        if (width != Width || Math.Abs(height) != Height || depth != 32)
            throw new InvalidDataException($"The capture is {width} by {height} at {depth} bits, not {Width} by {Height} at 32.");
        var pixels = new uint[Width * Height];
        for (var row = 0; row < Height; row++)
        {
            var y = height < 0 ? row : Height - 1 - row;
            for (var x = 0; x < Width; x++)
                pixels[y * Width + x] = BitConverter.ToUInt32(bytes, offset + (row * Width + x) * 4) & 0xFFFFFF;
        }
        return pixels;
    }
}
