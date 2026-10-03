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
              [--scenario <0-9>] [--mentality <0-3>] [--turns <26|52|104|208>] [--humans <slot[:modifier]>,...]
              [--end-turns <n>] [--seed <n>] [--dump-at-roll <n>] [--trace-calls <hex address>]
              [--orders <turn:slot:action:target:target_2:repeat>,...] [--hires <turn:offer_slot:sector>,...] [--sound]
              [--families <turn:player:slot:family>,...] [--raiders <turn:player>,...]
              [--search <turn:definition+definition...>,...]
              Modifiers: right_hands, visibility, hire_force, elite, islands, cash.
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
    var settings = new NewGameSettings(
        IntOption(args, "--scenario"), IntOption(args, "--mentality"), IntOption(args, "--turns"),
        Option(args, "--humans")?.Split(',').Select(entry =>
        {
            var parts = entry.Split(':');
            var modifier = parts.Length > 1 ? parts[1] : null;
            if (modifier is not null && !OriginalAddresses.ModifierNames.ContainsKey(modifier))
                throw new ArgumentException($"Unknown name modifier {modifier}.");
            return new HumanSlot(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), modifier);
        }).ToArray(),
        IntOption(args, "--end-turns") ?? 0,
        args.Contains("--trace-hires"),
        IntOption(args, "--seed"),
        IntOption(args, "--dump-at-roll"),
        HexOption(args, "--trace-calls"),
        Option(args, "--orders") is { } orders ? ParseOrders(orders) : null,
        args.Contains("--sound"),
        Option(args, "--hires") is { } hires ? ParseHires(hires) : null,
        ParsePlanning(Option(args, "--families"), Option(args, "--raiders")),
        Option(args, "--search") is { } search ? ParseSearch(search) : null);

    // --executable runs a copy from another path in the game directory, which escapes the
    // compatibility layers the registry ties to the installed path (docs/VALIDATION.md).
    var executable = Option(args, "--executable") ?? Path.Combine(game, "Chaos Overlords.exe");
    var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable)));
    if (hash != OriginalAddresses.ExecutableSha256)
    {
        Console.Error.WriteLine($"{executable} is not BLD-GOG-EN-1.1 (SHA-256 {hash}).");
        return 1;
    }

    Directory.CreateDirectory(output);
    using var session = new NewGameSession(executable, game, output, TimeSpan.FromSeconds(timeout), settings);
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
    string[]? settings = null;
    (string Name, string Value)[]? turns = null;
    foreach (var run in runs)
    {
        // Runs of one experiment differ only in the seed.
        var runSettings = StateExtractor.Settings(run);
        if (settings is not null && !settings.SequenceEqual(runSettings))
        {
            Console.Error.WriteLine($"{run} was recorded with other settings.");
            return 1;
        }

        var runTurns = StateExtractor.Turns(run);
        if (turns is not null && !turns.SequenceEqual(runTurns))
        {
            Console.Error.WriteLine($"{run} was recorded with other orders or turns.");
            return 1;
        }

        settings = runSettings;
        turns = runTurns;
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
        [
            new JsonObject { ["tick"] = 0, ["name"] = "command", ["value"] = "File, New Game (0x8101)" },
            .. settings!.Select(setting => new JsonObject { ["tick"] = 0, ["name"] = "setup", ["value"] = setting }),
            new JsonObject { ["tick"] = 0, ["name"] = "left_click", ["value"] = "Begin (416, 397)" },
            // An order is written and a Done pressed once the planning phase has settled, at the
            // roll count its run gives in done_at_roll.
            .. turns!.Select(turn => new JsonObject { ["tick"] = null, ["name"] = turn.Name, ["value"] = turn.Value }),
        ]),
        ["seeds"] = seeds,
        ["runs"] = runArray,
    };
    File.WriteAllText(output, StateExtractor.Serialize(fixture) + "\n");
    Console.WriteLine($"Wrote {runs.Length} runs to {output}.");
    return 0;
}

static int? IntOption(string[] args, string name) =>
    Option(args, name) is { } value ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : null;

// --orders turn:slot:action:target:target_2:repeat,... with repeat 0 or 1.
static IReadOnlyList<ProbeOrder> ParseOrders(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(order =>
    {
        var parts = order.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (parts.Length != 6) throw new FormatException($"An order needs six numbers: {order}");
        return new ProbeOrder(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5] != 0);
    }).ToArray();

// --hires turn:offer_slot:sector,... places the human's hires (ProbeHire).
static IReadOnlyList<ProbeHire> ParseHires(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(hire =>
    {
        var parts = hire.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (parts.Length != 3 || parts[1] is < 0 or > 2 || parts[2] is < 0 or > 63)
            throw new FormatException($"A hire needs a turn, an offer slot 0 to 2 and a sector 0 to 63: {hire}");
        return new ProbeHire(parts[0], parts[1], parts[2]);
    }).ToArray();

// --search turn:definition+definition+...,... sets the human's Search filter entries (ProbeSearch).
static IReadOnlyList<ProbeSearch> ParseSearch(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':');
        if (parts.Length != 2) throw new FormatException($"A Search write needs a turn and definitions: {entry}");
        var definitions = parts[1].Split('+').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (definitions.Any(definition => definition is < 0 or >= OriginalAddresses.SiteDefinitionCount))
            throw new FormatException($"A site definition is 0 to 21: {entry}");
        return new ProbeSearch(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), definitions);
    }).ToArray();

// --families turn:player:slot:family,... writes a planning record's family; --raiders
// turn:player,... sets a player's raider_mode (ProbePlanning).
static IReadOnlyList<ProbePlanning>? ParsePlanning(string? families, string? raiders)
{
    static int[] Numbers(string entry) =>
        entry.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    var writes = new List<ProbePlanning>();
    foreach (var entry in (families ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = Numbers(entry);
        if (parts.Length != 4 || parts[1] is < 1 or > 5 || parts[2] is < 0 or > 80 || parts[3] is < 0 or > 99)
            throw new FormatException($"A family write needs a turn, a player 1 to 5, a slot 0 to 80 and a family 0 to 99: {entry}");
        writes.Add(new ProbePlanning(parts[0], parts[1], parts[2], parts[3]));
    }
    foreach (var entry in (raiders ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = Numbers(entry);
        if (parts.Length != 2 || parts[1] is < 1 or > 5)
            throw new FormatException($"A raider needs a turn and a player 1 to 5: {entry}");
        writes.Add(new ProbePlanning(parts[0], parts[1], 0, ProbePlanning.Raider));
    }
    return writes.Count == 0 ? null : writes;
}

static uint? HexOption(string[] args, string name) =>
    Option(args, name) is { } value
        ? uint.Parse(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value,
            System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)
        : null;

static string? Option(string[] args, string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}
