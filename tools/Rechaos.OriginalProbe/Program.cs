using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rechaos.OriginalProbe;

// Runs the original game under a debugger to record what it does, for experiments and dynamic
// findings (docs/VALIDATION.md, "The probe"). A run's output holds the original's memory, so it is written
// outside the repository; `extract` takes only sanitized numbers from it for a fixture.
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The probe runs the original executable and needs Windows.");
    return 2;
}

return args.FirstOrDefault() switch
{
    "new-game" => NewGame(args),
    "extract" => Extract(args),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine(
        """
        Usage:
          Rechaos.OriginalProbe new-game --out <directory> [--game <install directory>] [--timeout <seconds>]
          Rechaos.OriginalProbe extract --experiment <EXP-ID> --out <fixture.json> <run directory>...
        """);
    return 2;
}

static int NewGame(string[] args)
{
    var game = Option(args, "--game") ?? @"C:\GOG Games\Chaos Overlords";
    var output = Option(args, "--out");
    var timeout = int.Parse(Option(args, "--timeout") ?? "180", System.Globalization.CultureInfo.InvariantCulture);
    if (output is null) return Usage();

    var executable = Path.Combine(game, "Chaos Overlords.exe");
    var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable)));
    if (hash != OriginalAddresses.ExecutableSha256)
    {
        Console.Error.WriteLine($"{executable} is not BLD-GOG-EN-1.1 (SHA-256 {hash}).");
        return 1;
    }

    Directory.CreateDirectory(output);
    using var session = new NewGameSession(executable, game, output, TimeSpan.FromSeconds(timeout));
    var trace = session.Run();
    var json = JsonSerializer.Serialize(trace, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(output, "trace.json"), json);
    Console.WriteLine($"Seed {trace.Seed}, {trace.Rolls.Count} rolls, state dumped: {trace.Dumped}.");
    return trace.Dumped ? 0 : 1;
}

static int Extract(string[] args)
{
    var experiment = Option(args, "--experiment");
    var output = Option(args, "--out");
    var runs = args.Skip(1).Where((_, i) => i >= 4).ToArray();
    if (experiment is null || output is null || runs.Length == 0 || args[1] != "--experiment" || args[3] != "--out")
        return Usage();

    var runArray = new JsonArray();
    var seeds = new JsonArray();
    foreach (var run in runs)
    {
        var extracted = StateExtractor.ExtractRun(run);
        seeds.Add(extracted["rng_state"]!.GetValue<int>());
        runArray.Add(extracted);
    }

    var fixture = new JsonObject
    {
        ["experiment"] = experiment,
        ["build"] = "BLD-GOG-EN-1.1",
        ["starting_state"] = null,
        ["recording_xxh3"] = null,
        ["clock"] = "roll",
        ["inputs"] = new JsonArray(
            new JsonObject { ["tick"] = 0, ["name"] = "command", ["value"] = "File, New Game (0x8101)" },
            new JsonObject { ["tick"] = 0, ["name"] = "left_click", ["value"] = "Begin (416, 397)" }),
        ["seeds"] = seeds,
        ["runs"] = runArray,
    };
    File.WriteAllText(output, StateExtractor.Serialize(fixture) + "\n");
    Console.WriteLine($"Wrote {runs.Length} runs to {output}.");
    return 0;
}

static string? Option(string[] args, string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}
