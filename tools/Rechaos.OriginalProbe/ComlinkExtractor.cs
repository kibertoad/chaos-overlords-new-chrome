using System.Text.Json.Nodes;

namespace Rechaos.OriginalProbe;

/// <summary>
/// Writes the fixture of a Comlink experiment from runs recorded with <c>new-game --comlink</c>: for
/// each step of the script, what the original did and, where the step kept one, every human
/// player's Comlink state. A message record (FMT-STATE-005) is written as
/// <c>[occupied, read, turn, sender, unk_a5, text]</c>, the text with its trailing spaces removed,
/// or null when all 160 bytes are 0. The texts are the ones the script typed; nothing of the game's
/// own content is written.
/// </summary>
internal static class ComlinkExtractor
{
    public static int Extract(string[] args)
    {
        var experiment = Option(args, "--experiment");
        var output = Option(args, "--out");
        var runs = args.Skip(5).ToArray();
        if (experiment is null || output is null || runs.Length == 0 || args[1] != "--experiment" || args[3] != "--out")
        {
            Console.Error.WriteLine("Usage: extract-comlink --experiment <EXP-ID> --out <fixture.json> <run directory>...");
            return 2;
        }

        string[]? settings = null;
        string[]? script = null;
        var seeds = new JsonArray();
        var runArray = new JsonArray();
        foreach (var run in runs)
        {
            var trace = JsonNode.Parse(File.ReadAllText(Path.Combine(run, "trace.json")))!;
            var runSettings = StateExtractor.Settings(run);
            var runScript = trace["Comlink"]!.AsArray().Select(step => step!["Step"]!.GetValue<string>()).ToArray();
            if (settings is not null && (!settings.SequenceEqual(runSettings) || !script!.SequenceEqual(runScript)))
            {
                Console.Error.WriteLine($"{run} was recorded with other settings or another script.");
                return 1;
            }
            if (trace["Dumped"]!.GetValue<bool>() is false)
            {
                Console.Error.WriteLine($"{run} did not finish its script.");
                return 1;
            }
            settings = runSettings;
            script = runScript;
            var humans = trace["Settings"]!["Humans"]!.AsArray().Select(human => human!["Slot"]!.GetValue<int>()).ToArray();
            var steps = new JsonArray();
            foreach (var step in trace["Comlink"]!.AsArray())
                steps.Add(Step(step!, humans));
            seeds.Add(trace["Seed"]!.GetValue<int>());
            runArray.Add(new JsonObject
            {
                ["rng_state"] = trace["Seed"]!.GetValue<int>(),
                ["humans"] = new JsonArray(humans.Select(slot => (JsonNode)slot).ToArray()),
                ["events"] = new JsonArray(),
                ["steps"] = steps,
            });
        }

        var fixture = new JsonObject
        {
            ["experiment"] = experiment,
            ["build"] = "BLD-GOG-EN-1.1",
            ["starting_state"] = null,
            ["recording_xxh3"] = null,
            ["clock"] = "step",
            ["inputs"] = new JsonArray(
            [
                new JsonObject { ["tick"] = 0, ["name"] = "command", ["value"] = "File, New Game (0x8101)" },
                .. settings!.Select(setting => new JsonObject { ["tick"] = 0, ["name"] = "setup", ["value"] = setting }),
                new JsonObject { ["tick"] = 0, ["name"] = "left_click", ["value"] = "Begin (416, 397)" },
                .. script!.Select((step, index) => new JsonObject { ["tick"] = index, ["name"] = "comlink", ["value"] = step }),
            ]),
            ["seeds"] = seeds,
            ["runs"] = runArray,
        };
        File.WriteAllText(output, StateExtractor.Serialize(fixture) + "\n");
        Console.WriteLine($"Wrote {runs.Length} runs to {output}.");
        return 0;
    }

    private static JsonObject Step(JsonNode step, int[] humans)
    {
        var result = new JsonObject
        {
            ["step"] = step["Step"]!.GetValue<string>(),
            ["active_player"] = step["ActivePlayer"]!.GetValue<int>(),
            ["elapsed_turns"] = step["ElapsedTurns"]!.GetValue<int>(),
            ["send_open"] = step["SendOpen"]!.GetValue<bool>(),
            ["view_open"] = step["ViewOpen"]!.GetValue<bool>(),
            ["entered"] = step["Entered"]!.DeepClone(),
            ["sounds"] = step["Sounds"]!.DeepClone(),
        };
        // FND-COMLINK-004: each showing of a message: the player and count passed, the cursor, and
        // the numbers drawn (page, count, year, week).
        if (step["Shows"]!.AsArray().Count > 0)
            result["shows"] = new JsonArray(step["Shows"]!.AsArray().Select(show => (JsonNode)new JsonObject
            {
                ["player"] = show!["Player"]!.GetValue<int>(),
                ["count"] = show["Count"]!.GetValue<int>(),
                ["cursor"] = show["Cursor"]!.GetValue<int>(),
                ["numbers"] = show["Numbers"]!.DeepClone(),
            }).ToArray());
        // FND-COMLINK-006: each drop of the leading read messages: player, count before, count after.
        if (step["Drops"]!.AsArray().Count > 0) result["drops"] = step["Drops"]!.DeepClone();
        if (step["Selected"] is JsonValue selected)
        {
            result["selected"] = new JsonArray(Convert.FromHexString(selected.GetValue<string>()).Select(value => (JsonNode)(int)value).ToArray());
            result["draft"] = Record(Convert.FromHexString(step["Draft"]!.GetValue<string>()));
        }
        if (step["Dump"] is JsonObject dump)
        {
            var records = new JsonObject();
            foreach (var player in humans)
            {
                var bytes = Convert.FromHexString(dump["Records"]![player]!.GetValue<string>());
                records[player.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new JsonArray(Enumerable.Range(0, 16)
                    .Select(index => (JsonNode)Record(bytes.AsSpan(index * 0xA6, 0xA6).ToArray())).ToArray());
            }
            result["comlink"] = new JsonObject
            {
                ["counts"] = dump["Counts"]!.DeepClone(),
                ["cursors"] = dump["Cursors"]!.DeepClone(),
                ["pending"] = dump["Pending"]!.GetValue<int>(),
                ["records"] = records,
            };
        }
        return result;
    }

    // FMT-STATE-005: occupied, read, turn, sender, unk_A5 and the text.
    private static JsonArray Record(byte[] record)
    {
        var text = record.AsSpan(5, 160);
        JsonNode? textNode = text.IndexOfAnyExcept((byte)0) < 0
            ? null
            : System.Text.Encoding.Latin1.GetString(text).TrimEnd(' ');
        return new JsonArray(record[0], record[1], (int)BitConverter.ToInt16(record, 2), record[4], record[0xA5], textNode);
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
