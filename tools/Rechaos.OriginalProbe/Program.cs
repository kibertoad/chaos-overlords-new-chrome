using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rechaos.OriginalProbe;

// Runs the original game under a debugger to record what it does, for experiments and dynamic
// findings (docs/validation/experiments.md, "The probe"). A run's output holds the original's memory, so it is written
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
    "extract-comlink" => ComlinkExtractor.Extract(args),
    "digest" => Digest(args),
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
              [--orders <turn[:player]:slot:action:target:target_2:repeat>,...] [--hires <turn[:player]:offer_slot:sector>,...] [--sound]
              [--families <turn:player:slot:family>,...] [--raiders <turn:player>,...] [--retire <turn:player>,...]
              [--cash <turn[-turn]:player:value>,...] [--force <turn:player:slot:force>,...] [--tolerance <turn:sector:value>,...]
              [--search <turn[:player]:definition+definition...>,...]
              [--finance <turn:sector>,...]
              [--deactivate <turn:player:slot>,...] [--pass-cards]
              [--time-limit <0-3>] [--expire-turns <turn>,...] [--delays <turn:ms>,...] [--menu <turn:after_ms:hold_ms>,...]
              [--clock-captures] [--capture] [--white-key]
              [--comlink <script file>]
              [--draw-values <hex address>=<int32>[/<int32>...],...]
              [--equip-lists] [--attack-lists] [--search-clicks <x:y>,...]
              [--hire-steps <drag:slot:sector|reject:slot|exit>,...]
              [--order-steps <open:sector|card:n:x:y:command|strip:x:y:command|dbl:x:y|back|exit|warn|wait:ms|type:TEXT|keys:TOKENS|shot:SCR-ID+...>,...] [--gang-markers]
              [--title-capture] [--credits-capture] [--setup-capture] [--setup-steps <strip:x:y|drag:x:y:x2:y2|name:TOKENS|shot>,...]
              [--setup-preferences <scenario>:<mentality>:<planning limit>]
              [--detailed-combat] [--pointer] [--sound-calls] [--watch-intro] [--waits] [--slides] [--saved <turn:value>,...] [--closes <saved:answer>,...]
              Modifiers: right_hands, visibility, hire_force, elite, islands, cash.
              An order, hire or Search write without a player acts for the first --humans slot.
          Rechaos.OriginalProbe extract --experiment <EXP-ID> --out <fixture.json> <run directory>... [--screens <SCR-ID>,...]
          Rechaos.OriginalProbe extract-comlink --experiment <EXP-ID> --out <fixture.json> <run directory>...
          Rechaos.OriginalProbe digest --fixture <fixture.json> --run <n> --screens <SCR-ID>,...
        """);
    return 2;
}

static int NewGame(string[] args)
{
    var game = Option(args, "--game") ?? @"C:\GOG Games\Chaos Overlords";
    var output = Option(args, "--out");
    var timeout = int.Parse(Option(args, "--timeout") ?? "180", System.Globalization.CultureInfo.InvariantCulture);
    if (output is null) return Usage();
    if (IntOption(args, "--time-limit") is { } timeLimit and not (>= 0 and <= 3))
        throw new ArgumentException($"--time-limit takes 0 to 3, not {timeLimit}.");
    // --expire-turns waits for the planning time to run out, which needs a limit to run out.
    if (Option(args, "--expire-turns") is not null && IntOption(args, "--time-limit") is not (>= 1 and <= 3))
        throw new ArgumentException("--expire-turns needs --time-limit 1, 2 or 3.");
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
        ParsePlanning(Option(args, "--families"), Option(args, "--raiders"), Option(args, "--retire"), Option(args, "--cash"),
            Option(args, "--force"), Option(args, "--tolerance"), Option(args, "--deactivate")),
        Option(args, "--finance") is { } finance ? ParseFinance(finance) : null,
        Option(args, "--search") is { } search ? ParseSearch(search) : null,
        IntOption(args, "--time-limit"),
        Option(args, "--expire-turns")?.Split(',').Select(value =>
            int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
        Option(args, "--comlink") is { } script ? File.ReadAllLines(script) : null,
        args.Contains("--capture"),
        args.Contains("--white-key"),
        Option(args, "--draw-values") is { } drawValues ? ParseDrawValues(drawValues) : null,
        args.Contains("--equip-lists"),
        args.Contains("--attack-lists"),
        Option(args, "--search-clicks") is { } searchClicks ? ParseClicks(searchClicks) : null,
        Option(args, "--hire-steps") is { } hireSteps ? ParseHireSteps(hireSteps) : null,
        Option(args, "--order-steps") is { } orderSteps ? ParseOrderSteps(orderSteps) : null,
        args.Contains("--gang-markers"),
        args.Contains("--title-capture"),
        args.Contains("--credits-capture"),
        args.Contains("--setup-capture"),
        Option(args, "--setup-steps") is { } setupSteps ? ParseSetupSteps(setupSteps) : null,
        args.Contains("--detailed-combat"),
        args.Contains("--pointer"),
        args.Contains("--sound-calls"),
        args.Contains("--watch-intro"),
        args.Contains("--waits"),
        args.Contains("--slides"),
        Option(args, "--saved") is { } saved ? ParseSavedWrites(saved) : null,
        Option(args, "--closes") is { } closes ? ParseCloses(closes) : null,
        args.Contains("--pass-cards"),
        Option(args, "--delays") is { } delays ? ParseDelays(delays) : null,
        Option(args, "--menu") is { } menus ? ParseMenus(menus) : null,
        args.Contains("--clock-captures"),
        Option(args, "--setup-preferences") is { } setupPreferences
            ? ProbeSetupPreferences.Parse(setupPreferences)
            : args.Contains("--setup-capture") || args.Contains("--setup-steps") ? ProbeSetupPreferences.GogInstallation : null)
        .WithActingPlayers();
    if (settings.SetupPreferences is not null && !settings.SetupCopied)
        throw new ArgumentException("--setup-preferences needs --setup-capture or --setup-steps.");
    // An order, hire or Search write acts for a human of the run: the probe writes it into that
    // player's records, and the fixture names the player in the input.
    foreach (var write in settings.Search ?? [])
        if (!settings.HumanSlots.Contains(write.Player))
            throw new ArgumentException($"Player {write.Player} of a Search write is not a --humans slot.");
    // RULE-SETUP-008: the probe plays inputs only in the first --humans slot's planning. Another
    // human plans behind a Ready card, which a hot-seat run presses before a bare Done, and that
    // press refills the human's offers, so an order or hire written for that human before then may
    // not be what it plans with.
    foreach (var player in (settings.Orders ?? []).Select(order => order.Player)
                 .Concat((settings.Hires ?? []).Select(hire => hire.Player)))
        if (player != settings.FirstHuman)
            throw new ArgumentException(
                $"Player {player} of an order or hire is not the first --humans slot, the only human whose planning the probe plays.");
    // RULE-SETUP-008: a hot-seat run writes the first --humans slot's orders and ends each round
    // at that slot's hand-off card, which is the round's first only when it is the lowest slot.
    if (settings.HotSeat && settings.Humans![0].Slot != settings.Humans.Min(human => human.Slot))
        throw new ArgumentException("A run with several humans and --end-turns plays the first --humans slot; list the lowest slot first.");
    if (settings.Menus is not null && settings.TimeLimit is not (>= 1 and <= 3))
        throw new ArgumentException("--menu times the menu bar on the planning clock, which needs --time-limit 1, 2 or 3.");
    // RULE-EQUIP-004, RULE-ATTACK-002: the probe builds the lists of the first --humans slot, and
    // the fixture does not say whose they are, so the replay reads them as the lowest human slot's.
    // A first slot that is not the lowest would compare one player's lists with another player's
    // gangs.
    if ((settings.EquipLists || settings.AttackLists) && settings.Humans is { Count: > 1 } listed
        && listed[0].Slot != listed.Min(human => human.Slot))
        throw new ArgumentException("--equip-lists and --attack-lists record the first --humans slot; list the lowest slot first.");

    // --executable runs a copy from another path in the game directory, which escapes the
    // compatibility layers the registry ties to the installed path (docs/validation/experiments.md).
    var executable = Option(args, "--executable") ?? Path.Combine(game, "Chaos Overlords.exe");
    var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(executable)));
    if (hash != OriginalAddresses.ExecutableSha256)
    {
        Console.Error.WriteLine($"{executable} is not BLD-GOG-EN-1.1 (SHA-256 {hash}).");
        return 1;
    }
    if (settings.DrawValues is { } drawn && DrawValuesProblem(drawn, settings.Humans, executable) is { } problem)
    {
        Console.Error.WriteLine(problem);
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
    // --screens SCR-ID,... names the screen entries whose elements a capture's digests cover
    // (CaptureScreen); it may stand anywhere after the run directories' options.
    var screens = CaptureScreen.Load(Option(args, "--screens"));
    if (Array.IndexOf(args, "--screens") is var at and >= 0)
        args = args.Where((_, index) => index != at && index != at + 1).ToArray();
    if (StateExtractor.ExtractArguments(args) is not { } arguments)
        return Usage();
    var (experiment, output, runs) = arguments;

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
            // The inputs list only the turns a run played, and a fixture holds one list for all
            // its runs, so runs whose matches end on different turns cannot share a fixture.
            Console.Error.WriteLine(
                $"{run} was recorded with other orders or turns, or its match ended on another turn than the runs before it.");
            return 1;
        }

        settings = runSettings;
        turns = runTurns;
        var extracted = StateExtractor.ExtractRun(run, screens);
        seeds.Add(extracted["rng_state"]!.GetValue<int>());
        runArray.Add(extracted);
    }

    // An order is written and a Done pressed once the planning phase has settled, at the roll count
    // its run gives in done_at_roll.
    StateExtractor.WriteFixture(output, experiment, "roll", settings!,
        turns!.Select(turn => new JsonObject { ["tick"] = null, ["name"] = turn.Name, ["value"] = turn.Value }),
        seeds, runArray);
    Console.WriteLine($"Wrote {runs.Length} runs to {output}.");
    return 0;
}

// digest: adds element digests to a capture a fixture already records, from the bitmap kept under
// GAME_DIR/captures (CaptureFixture.AddScreens).
static int Digest(string[] args)
{
    var path = Option(args, "--fixture");
    var run = IntOption(args, "--run");
    var screens = CaptureScreen.Load(Option(args, "--screens"));
    if (path is null || run is null || screens.Count == 0) return Usage();
    var fixture = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    var count = CaptureFixture.AddScreens(fixture["runs"]![run.Value]!.AsObject(), screens);
    File.WriteAllText(path, StateExtractor.Serialize(fixture) + "\n");
    Console.WriteLine($"Wrote the digests of {count} elements to run {run} of {path}.");
    return 0;
}

static int? IntOption(string[] args, string name) =>
    Option(args, name) is { } value ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : null;

// The player after the turn of an --orders, --hires or --search entry when the entry gives one,
// with the numbers after it; -1, the first --humans slot, when it does not.
static (int Player, int[] Numbers) ActingPlayer(int[] afterTurn, int withoutPlayer, string entry)
{
    if (afterTurn.Length == withoutPlayer) return (-1, afterTurn);
    if (afterTurn.Length == withoutPlayer + 1 && afterTurn[0] is >= 0 and <= 5) return (afterTurn[0], afterTurn[1..]);
    throw new FormatException($"Expected a turn, an optional player 0 to 5 and {withoutPlayer} more number(s): {entry}");
}

// --orders turn[:player]:slot:action:target:target_2:repeat,... with repeat 0 or 1.
static IReadOnlyList<ProbeOrder> ParseOrders(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(order =>
    {
        var parts = order.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var (player, rest) = ActingPlayer(parts[1..], 5, order);
        return new ProbeOrder(parts[0], rest[0], rest[1], rest[2], rest[3], rest[4] != 0, player);
    }).ToArray();

// --hires turn[:player]:offer_slot:sector,... places a human's hires (ProbeHire).
static IReadOnlyList<ProbeHire> ParseHires(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(hire =>
    {
        var parts = hire.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var (player, rest) = ActingPlayer(parts[1..], 2, hire);
        if (rest[0] is < 0 or > 2 || rest[1] is < 0 or > 63)
            throw new FormatException($"A hire needs a turn, an optional player, an offer slot 0 to 2 and a sector 0 to 63: {hire}");
        return new ProbeHire(parts[0], rest[0], rest[1], player);
    }).ToArray();

// --search turn[:player]:definition+definition+...,... sets a human's Search filter entries (ProbeSearch).
static IReadOnlyList<ProbeSearch> ParseSearch(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':');
        if (parts.Length is not (2 or 3)) throw new FormatException($"A Search write needs a turn, an optional player and definitions: {entry}");
        var definitions = parts[^1].Split('+').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (definitions.Any(definition => definition is < 0 or >= OriginalAddresses.SiteDefinitionCount))
            throw new FormatException($"A site definition is 0 to 21: {entry}");
        var player = parts.Length == 3 ? int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : -1;
        if (parts.Length == 3 && player is < 0 or > 5) throw new FormatException($"A player is 0 to 5: {entry}");
        return new ProbeSearch(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), definitions, player);
    }).ToArray();

// --search-clicks x:y,... posts a click at each client point after the dump (SearchClickRecord).
static IReadOnlyList<ProbeClick> ParseClicks(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':');
        if (parts.Length != 2) throw new FormatException($"A click needs x and y: {entry}");
        return new ProbeClick(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }).ToArray();

// --saved turn:value,... writes match_saved before the Done press of the turn, or at the dump for
// the turn after the last (ProbeSavedWrite).
static IReadOnlyList<ProbeSavedWrite> ParseSavedWrites(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return parts is [>= 1, 0 or 1]
            ? new ProbeSavedWrite(parts[0], parts[1])
            : throw new FormatException($"A saved write needs a turn from 1 and a value 0 or 1: {entry}");
    }).ToArray();

// --closes saved:answer,... closes the window after the dump and answers the dialog the close
// opens: saved is -, 0 or 1 (written to match_saved first); answer 1w saves first and the save is
// written, 1c saves first and the save is cancelled, 2 cancels and 3 goes on without saving
// (ProbeClose).
static IReadOnlyList<ProbeClose> ParseCloses(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':');
        int? saved = parts.Length == 2 && parts[0] is "0" or "1" ? int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) : null;
        ProbeClose? close = parts.Length == 2 && (saved is not null || parts[0] == "-")
            ? parts[1] switch
            {
                "1w" => new ProbeClose(saved, 1, 1),
                "1c" => new ProbeClose(saved, 1, 0),
                "2" => new ProbeClose(saved, 2),
                "3" => new ProbeClose(saved, 3),
                _ => null
            }
            : null;
        return close ?? throw new FormatException(
            $"A close is saved:answer, saved -, 0 or 1 and answer 1w, 1c, 2 or 3: {entry}");
    }).ToArray();

// --hire-steps drag:slot:sector,reject:slot,exit,... drags offers onto map sectors, presses their
// Reject crosses and presses a result panel's Exit after the dump (ProbeHireStep).
static IReadOnlyList<ProbeHireStep> ParseHireSteps(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':');
        var numbers = parts.Skip(1).Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return parts[0] switch
        {
            "drag" when numbers is [>= 0 and < 3, >= 0 and < 64] => new ProbeHireStep(numbers[0], numbers[1]),
            "reject" when numbers is [>= 0 and < 3] => new ProbeHireStep(numbers[0], -2),
            "exit" when numbers is [] => new ProbeHireStep(-1, -1),
            _ => throw new FormatException($"A hire step is drag:slot:sector, reject:slot or exit: {entry}"),
        };
    }).ToArray();

// --order-steps open:sector,card:n:x:y:command,strip:x:y:command,back,exit,... opens a sector view,
// presses a gang card's strip at (x, y) within the card or the window at (x, y), and answers the
// popup menu with the command, 0 for none, after the dump (ProbeOrderStep).
static IReadOnlyList<ProbeOrderStep> ParseOrderSteps(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':');
        // shot:SCR-ID+SCR-ID names the screen entries the capture is compared at.
        if (parts is ["shot", var screens] && screens.Length > 0)
            return new ProbeOrderStep("shot", -1, 0, 0, 0, screens.Replace('+', ','));
        // keys:TOKENS presses virtual keys with the Shift test's result given (NewGameSession.Keys).
        if (parts is ["keys", var keys] && keys.Length > 0)
            return new ProbeOrderStep("keys", -1, 0, 0, 0, Text: NewGameSession.CheckedKeyTokens(keys, characters: false));
        // type:TEXT presses a key for each character: upper-case letters, digits and spaces.
        if (parts is ["type", var text] && text.Length > 0
            && text.All(character => character is ' ' or (>= '0' and <= '9') or (>= 'A' and <= 'Z')))
            return new ProbeOrderStep("type", -1, 0, 0, 0, Text: text);
        var numbers = parts.Skip(1).Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return parts[0] switch
        {
            "open" when numbers is [>= 0 and < 64] => new ProbeOrderStep("open", numbers[0], 0, 0, 0),
            "card" when numbers is [>= 0 and < 6, >= 0 and < 74, >= 0 and < 110, >= 0] =>
                new ProbeOrderStep("card", numbers[0], numbers[1], numbers[2], numbers[3]),
            "strip" when numbers is [>= 0 and < 640, >= 0 and < 480, >= 0] =>
                new ProbeOrderStep("strip", -1, numbers[0], numbers[1], numbers[2]),
            "dbl" when numbers is [>= 0 and < 640, >= 0 and < 480] =>
                new ProbeOrderStep("dbl", -1, numbers[0], numbers[1], 0),
            "back" or "exit" or "warn" when numbers is [] => new ProbeOrderStep(parts[0], -1, 0, 0, 0),
            "down" or "move" or "up" or "rdown" or "rup" when numbers is [>= 0 and < 640, >= 0 and < 480] =>
                new ProbeOrderStep(parts[0], -1, numbers[0], numbers[1], 0),
            "wait" when numbers is [> 0] => new ProbeOrderStep("wait", -1, 0, 0, numbers[0]),
            _ => throw new FormatException($"An order step is open:sector, card:n:x:y:command, strip:x:y:command, dbl:x:y, back, exit, warn, wait:ms, type:TEXT, keys:TOKENS, down:x:y, move:x:y, up:x:y, rdown:x:y, rup:x:y or shot:SCR-ID+...: {entry}"),
        };
    }).ToArray();

// --setup-steps strip:x:y,drag:x:y:x2:y2,shot,... presses window points of the setup screen, or
// drags from one to another, and copies it, each copy compared at SCR-SETUP-001.
static IReadOnlyList<ProbeOrderStep> ParseSetupSteps(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':');
        if (parts is ["shot"]) return new ProbeOrderStep("shot", -1, 0, 0, 0, "SCR-SETUP-001");
        // name:TOKENS types into the name editor of card 0 (NewGameSession.Keys).
        if (parts is ["name", var keys] && keys.Length > 0)
            return new ProbeOrderStep("name", -1, 0, 0, 0, Text: NewGameSession.CheckedKeyTokens(keys, characters: true));
        var numbers = parts.Skip(1).Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        const int width = CaptureFixture.Width, height = CaptureFixture.Height;
        return (parts[0], numbers) switch
        {
            ("strip", [>= 0 and < width, >= 0 and < height]) =>
                new ProbeOrderStep("strip", -1, numbers[0], numbers[1], 0),
            // drag:x:y:x2:y2 presses at (x, y), moves to (x2, y2) with the button down and releases
            // there; Target and Choice carry the release point.
            ("drag", [>= 0 and < width, >= 0 and < height, >= 0 and < width, >= 0 and < height]) =>
                new ProbeOrderStep("drag", numbers[2], numbers[0], numbers[1], numbers[3]),
            _ => throw new FormatException($"A setup step is strip:x:y, drag:x:y:x2:y2, name:TOKENS or shot: {entry}"),
        };
    }).ToArray();

// --finance turn:sector,... opens the Financial panel before that turn's Done, the City variant for
// sector -1 (ProbeFinance).
static IReadOnlyList<ProbeFinance> ParseFinance(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(panel =>
    {
        var parts = panel.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (parts.Length != 2 || parts[1] is < -1 or > 63)
            throw new FormatException($"A Financial panel needs a turn and a sector -1 to 63: {panel}");
        return new ProbeFinance(parts[0], parts[1]);
    }).ToArray();

// --draw-values address=value/value...,... writes 32-bit values whenever the planning-entry function
// starts drawing the console, the nth value at its nth call (ProbeDrawValue).
static IReadOnlyList<ProbeDrawValue> ParseDrawValues(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split('=');
        if (parts.Length != 2) throw new FormatException($"A drawn value needs an address and a value: {entry}");
        var address = parts[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? parts[0][2..] : parts[0];
        return new ProbeDrawValue(
            uint.Parse(address, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
            parts[1].Split('/').Select(number => int.Parse(number, System.Globalization.CultureInfo.InvariantCulture)).ToArray());
    }).ToArray();

// The calls of the planning-entry function are counted over every human's entries, and each
// human's console draws its own slot, so with a second human the nth value would not reach the nth
// entry of the seat it was meant for. An address outside the writable sections would overwrite
// code or a constant instead of a value the console draws.
static string? DrawValuesProblem(IReadOnlyList<ProbeDrawValue> values, IReadOnlyList<HumanSlot>? humans, string executable)
{
    if (humans is { Count: > 1 })
        return "--draw-values counts the planning entries of one human; give at most one --humans slot.";
    var writable = PeSection.Read(executable, out _).Where(section => section.IsWritable).ToArray();
    foreach (var value in values)
    {
        if (!writable.Any(section => value.Address >= section.VirtualAddress
            && (ulong)value.Address + 4 <= (ulong)section.VirtualAddress + section.VirtualSize))
            return $"--draw-values address 0x{value.Address:X8} does not lie in a writable section of the executable.";
    }
    return null;
}

// --families turn:player:slot:family,... writes a planning record's family; --raiders
// turn:player,... sets a player's raider_mode; --retire turn:player,... clears a player's
// player_active; --cash turns:player:value,... sets a player's cash before the Done press of each
// turn, turns being one turn or a range first-last; --force turn:player:slot:force,... sets a gang's
// force; --tolerance turn:sector:value,... sets a sector's base_tolerance (ProbePlanning).
static IReadOnlyList<ProbePlanning>? ParsePlanning(string? families, string? raiders, string? retired, string? cash,
    string? force, string? tolerance, string? deactivated)
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
    foreach (var entry in (retired ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = Numbers(entry);
        if (parts.Length != 2 || parts[1] is < 0 or > 5)
            throw new FormatException($"A retired player needs a turn and a player 0 to 5: {entry}");
        writes.Add(new ProbePlanning(parts[0], parts[1], 0, ProbePlanning.Retired));
    }
    foreach (var entry in (cash ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var range = entry.Split(':', 2);
        var turns = range[0].Split('-').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        int[] parts = range.Length == 2 ? Numbers(range[1]) : [];
        if (parts.Length != 2 || parts[0] is < 0 or > 5 || turns is not ([>= 1] or [>= 1, _]) || turns[^1] < turns[0])
            throw new FormatException($"A cash write needs a turn or a range of turns from 1, a player 0 to 5 and a value: {entry}");
        for (var turn = turns[0]; turn <= turns[^1]; turn++)
            writes.Add(new ProbePlanning(turn, parts[0], 0, ProbePlanning.Cash, parts[1]));
    }
    foreach (var entry in (force ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = Numbers(entry);
        if (parts.Length != 4 || parts[0] < 1 || parts[1] is < 0 or > 5 || parts[2] is < 0 or > 80 || parts[3] is < -128 or > 127)
            throw new FormatException($"A force write needs a turn from 1, a player 0 to 5, a slot 0 to 80 and a value -128 to 127: {entry}");
        writes.Add(new ProbePlanning(parts[0], parts[1], parts[2], ProbePlanning.Force, parts[3]));
    }
    foreach (var entry in (tolerance ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = Numbers(entry);
        if (parts.Length != 3 || parts[0] < 1 || parts[1] is < 0 or > 63 || parts[2] is < -128 or > 127)
            throw new FormatException($"A tolerance write needs a turn from 1, a sector 0 to 63 and a value -128 to 127: {entry}");
        writes.Add(new ProbePlanning(parts[0], 0, parts[1], ProbePlanning.Tolerance, parts[2]));
    }
    foreach (var entry in (deactivated ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = Numbers(entry);
        if (parts.Length != 3 || parts[0] < 1 || parts[1] is < 0 or > 5 || parts[2] is < 0 or > 80)
            throw new FormatException($"A deactivated gang needs a turn from 1, a player 0 to 5 and a roster slot 0 to 80: {entry}");
        writes.Add(new ProbePlanning(parts[0], parts[1], parts[2], ProbePlanning.Deactivated));
    }
    return writes.Count == 0 ? null : writes;
}

// --delays turn:ms,... waits that long before the Done press of the turn (ProbeDelay).
static IReadOnlyList<ProbeDelay> ParseDelays(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return parts is [>= 1, > 0]
            ? new ProbeDelay(parts[0], parts[1])
            : throw new FormatException($"A delay needs a turn from 1 and milliseconds above 0: {entry}");
    }).ToArray();

// --menu turn:after_ms:hold_ms,... holds the menu bar open during the turn's planning (ProbeMenu).
static IReadOnlyList<ProbeMenu> ParseMenus(string value) =>
    value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(entry =>
    {
        var parts = entry.Split(':').Select(part => int.Parse(part, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return parts is [>= 1, >= 0, > 0]
            ? new ProbeMenu(parts[0], parts[1], parts[2])
            : throw new FormatException($"A menu hold needs a turn from 1, an opening time and a hold in milliseconds: {entry}");
    }).ToArray();

static uint? HexOption(string[] args, string name) =>
    Option(args, name) is { } value
        ? uint.Parse(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value,
            System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)
        : null;

static string? Option(string[] args, string name) => StateExtractor.Option(args, name);
