using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rechaos.OriginalProbe;

/// <summary>
/// Reads the match state out of a dump of the original's .data section and writes it as the
/// fixture run of the documentation standard: the seed, every roll, and the end state as
/// (format, record, field, value) and (term, index, value) rows. Every address and layout is the
/// spec's: FMT-STATE-001 for gangs, FMT-STATE-002 and FMT-STATE-004 for sectors, and the glossary
/// for the other terms. Only numbers are written; names and texts stay out.
/// </summary>
internal sealed class StateExtractor
{
    private const uint DataStart = 0x00482000;
    private readonly byte[] _data;

    private StateExtractor(byte[] data) => _data = data;

    public static JsonObject ExtractRun(string runDirectory, IReadOnlyList<CaptureScreen> screens)
    {
        var trace = JsonNode.Parse(File.ReadAllText(Path.Combine(runDirectory, "trace.json")))!;
        var extractor = new StateExtractor(File.ReadAllBytes(Path.Combine(runDirectory, $"data-{DataStart:X8}.bin")));
        var doneAtRoll = new JsonArray();
        foreach (var count in trace["RollsAtDone"]?.AsArray() ?? []) doneAtRoll.Add(count!.GetValue<int>());
        var rolls = new JsonArray();
        foreach (var roll in trace["Rolls"]!.AsArray())
            rolls.Add(new JsonArray(roll!["Call"]!.GetValue<string>(), roll["Bound"]!.GetValue<int>(), roll["Result"]!.GetValue<int>()));
        var run = new JsonObject
        {
            ["rng_state"] = trace["Seed"]!.GetValue<int>(),
            ["done_at_roll"] = doneAtRoll,
            ["rolls"] = rolls,
            ["events"] = new JsonArray(),
            ["end_state"] = extractor.EndState(),
        };
        // The rolls the state dump follows, when steps after it made more.
        if (trace["Notes"]?.AsArray().Select(note => note!.GetValue<string>())
                .FirstOrDefault(note => note.StartsWith("rolls_at_dump ", StringComparison.Ordinal)) is { } atDump
            && int.Parse(atDump["rolls_at_dump ".Length..], System.Globalization.CultureInfo.InvariantCulture) is var dumpRolls
            && dumpRolls < rolls.Count)
            run["rolls_at_dump"] = dumpRolls;
        // FND-AWARDS-005: the players of the endgame's rows in drawing order, and each row's kind.
        if (trace["Endgame"] is JsonObject endgame)
            run["endgame_rows"] = new JsonObject
            {
                ["arguments"] = endgame["Arguments"]!.DeepClone(),
                ["players"] = endgame["Rows"]!.DeepClone(),
                ["kinds"] = endgame["Kinds"]!.DeepClone(),
            };
        // FND-SEARCH-006: the site markers of the last city redraw before the dump, each as
        // definition, sector, ordinal and controlled flag, with the viewing player.
        if (trace["Markers"] is JsonObject markers)
            run["city_markers"] = new JsonObject
            {
                ["viewer"] = markers["Viewer"]!.GetValue<int>(),
                ["markers"] = markers["Markers"]!.DeepClone(),
            };
        // FND-FINANCE-003: the nine numbers of each Financial panel the run opened, in drawing order,
        // with the sector the panel function was passed.
        if (trace["Finance"] is JsonArray finance)
        {
            var panels = new JsonArray();
            foreach (var panel in finance)
                panels.Add(new JsonObject
                {
                    ["turn"] = panel!["Turn"]!.GetValue<int>(),
                    ["sector"] = panel["PanelSector"]!.GetValue<int>(),
                    ["values"] = new JsonArray(panel["Values"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
                });
            run["finance"] = panels;
        }
        // RULE-EQUIP-004, FND-EQUIP-008: the list the Equip panel's builder filled for each category
        // of each of the first human's living gangs, with the Tech Level it was passed.
        if (trace["EquipLists"] is JsonArray equipLists)
            run["equip_lists"] = new JsonArray(equipLists.Select(list => (JsonNode)new JsonObject
            {
                ["slot"] = list!["Slot"]!.GetValue<int>(),
                ["category"] = list["Category"]!.GetValue<int>(),
                ["tech_level"] = list["TechLevel"]!.GetValue<int>(),
                ["items"] = new JsonArray(list["Items"]!.AsArray().Select(item => (JsonNode)item!.GetValue<int>()).ToArray()),
            }).ToArray());
        // RULE-ATTACK-002, FND-ATTACK-006: the targets the Attack picker's roster builder filled for
        // each opponent and each of the first human's living gangs, with the sector it was passed.
        if (trace["AttackLists"] is JsonArray attackLists)
            run["attack_lists"] = new JsonArray(attackLists.Select(list => (JsonNode)new JsonObject
            {
                ["slot"] = list!["Slot"]!.GetValue<int>(),
                ["sector"] = list["Sector"]!.GetValue<int>(),
                ["opponent"] = list["Opponent"]!.GetValue<int>(),
                ["targets"] = new JsonArray(list["Targets"]!.AsArray().Select(target => (JsonNode)target!.GetValue<int>()).ToArray()),
            }).ToArray());
        // RULE-COMBAT-004, RULE-AUDIO-009: each clip Detailed Combat played, as the probe read it
        // when the clip player was entered (FND-COMBAT-011, FND-AUDIO-013).
        if (trace["CombatClips"] is JsonArray combatClips)
            run["combat_clips"] = new JsonArray(combatClips.Select(clip => (JsonNode)new JsonObject
            {
                ["after_roll"] = clip!["AfterRoll"]!.GetValue<int>(),
                ["focal"] = clip["Focal"]!.GetValue<int>(),
                ["other"] = clip["Other"]!.GetValue<int>(),
                ["hold"] = clip["Hold"]!.GetValue<int>(),
                ["focal_bar"] = clip["FocalBar"]!.GetValue<int>(),
                ["other_bar"] = clip["OtherBar"]!.GetValue<int>(),
                ["sounds"] = new JsonArray(clip["Sounds"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
                ["played"] = clip["Played"]!.GetValue<bool>(),
            }).ToArray());
        // RULE-COMBAT-004: each call of the presentation, with its flag, its clips and the effect
        // slots it played itself (FND-COMBAT-010).
        if (trace["CombatPresentations"] is JsonArray combatPresentations)
            run["combat_presentations"] = new JsonArray(combatPresentations.Select(presentation => (JsonNode)new JsonObject
            {
                ["after_roll"] = presentation!["AfterRoll"]!.GetValue<int>(),
                ["automatic"] = presentation["Automatic"]!.GetValue<int>(),
                ["first_clip"] = presentation["FirstClip"]!.GetValue<int>(),
                ["clips"] = presentation["Clips"]!.GetValue<int>(),
                ["returned"] = presentation["Returned"]!.GetValue<bool>(),
                ["sounds"] = new JsonArray(presentation["Sounds"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
            }).ToArray());
        // RULE-UI-007: each call of the cursor helper as after_roll, done, shape, force and the
        // call's address (FND-UI-034).
        if (trace["PointerCalls"] is JsonArray pointerCalls)
            run["pointer_calls"] = new JsonArray(pointerCalls.Select(call => (JsonNode)new JsonArray(
                call!["AfterRoll"]!.GetValue<int>(), call["Done"]!.GetValue<int>(), call["Shape"]!.GetValue<int>(),
                call["Force"]!.GetValue<int>(), (int)call["Call"]!.GetValue<uint>())).ToArray());
        // RULE-AUDIO-006: each call of the play helper as after_roll, done, slot and the call's
        // address (FND-AUDIO-006), with effects_enabled as the run read it at each call, each Done
        // press and its end: the effects wrapper calls the helper only while it is set
        // (FND-AUDIO-002), so the calls cannot be read without it, and a run that does not record
        // it is refused.
        if (trace["SoundCalls"] is JsonArray soundCalls)
        {
            if (trace["EffectsEnabled"] is not JsonValue effectsEnabled)
                throw new InvalidDataException(
                    $"{runDirectory} records sound calls but not whether effects were enabled, or read different values during the run.");
            run["effects_enabled"] = effectsEnabled.GetValue<bool>();
            run["sound_calls"] = new JsonArray(soundCalls.Select(call => (JsonNode)new JsonArray(
                call!["AfterRoll"]!.GetValue<int>(), call["Done"]!.GetValue<int>(), call["Slot"]!.GetValue<int>(),
                (int)call["Call"]!.GetValue<uint>())).ToArray());
        }
        // RULE-VIDEO-001: each intro movie with its header's frame count, the frame counter at each
        // frame shown, the milliseconds from the first movie's first frame to each, and the counter at
        // its close.
        if (trace["IntroMovies"] is JsonArray introMovies)
            run["intro_movies"] = new JsonArray(introMovies.Select(movie => (JsonNode)new JsonObject
            {
                ["name"] = movie!["Name"]!.GetValue<string>(),
                ["frames"] = movie["Frames"]!.GetValue<int>(),
                ["shown"] = new JsonArray(movie["Shown"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
                ["milliseconds"] = new JsonArray(movie["Milliseconds"]!.AsArray().Select(value => (JsonNode)value!.GetValue<long>()).ToArray()),
                ["closed_at"] = movie["ClosedAt"]!.GetValue<int>(),
            }).ToArray());
        // RULE-TIMER-004: from the dump on, each tick of the presentation clock and each call of the
        // wait as ticks, the call's address and the milliseconds of its start and return, both
        // counted from the same clock as the ticks (FND-TIMER-002).
        if (trace["Ticks"] is JsonArray ticks)
            run["ticks"] = new JsonArray(ticks.Select(tick => (JsonNode)tick!.GetValue<long>()).ToArray());
        if (trace["Waits"] is JsonArray waits)
            run["waits"] = new JsonArray(waits.Select(wait => (JsonNode)new JsonArray(
                wait!["Ticks"]!.GetValue<int>(), (int)wait["Call"]!.GetValue<uint>(),
                wait["Started"]!.GetValue<long>(), wait["Returned"]!.GetValue<long>())).ToArray());
        // RULE-UI-003: from the dump on, each slide-in as the benchmark count, the travel and the
        // offset of each copy (FND-UI-011).
        if (trace["Slides"] is JsonArray slides)
            run["slides"] = new JsonArray(slides.Select(slide => (JsonNode)new JsonObject
            {
                ["benchmark"] = slide!["Benchmark"]!.GetValue<int>(),
                ["travel"] = slide["Travel"]!.GetValue<int>(),
                ["offsets"] = new JsonArray(slide["Offsets"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
            }).ToArray());
        // RULE-UI-015: each write of match_saved, with the value the game held before it.
        if (trace["SavedWrites"] is JsonArray savedWrites)
            run["saved_writes"] = new JsonArray(savedWrites.Select(write => (JsonNode)new JsonObject
            {
                ["turn"] = write!["Turn"]!.GetValue<int>(),
                ["before"] = write["Before"]!.GetValue<int>(),
                ["value"] = write["Value"]!.GetValue<int>(),
            }).ToArray());
        // RULE-UI-015: each close of the window after the dump, with match_saved and the no-match byte
        // as it was posted, the answer and save result given, the dialogs opened, the saves called
        // and the store of quit_requested reached, or 0x00000000.
        if (trace["Closes"] is JsonArray closes)
            run["closes"] = new JsonArray(closes.Select(close => (JsonNode)new JsonObject
            {
                ["saved"] = close!["Saved"]!.GetValue<int>(),
                ["no_match"] = close["NoMatch"]!.GetValue<int>(),
                ["answer"] = close["Answer"]!.GetValue<int>(),
                ["save_result"] = close["SaveResult"]!.GetValue<int>(),
                ["dialogs"] = new JsonArray(close["Dialogs"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
                ["saves"] = close["Saves"]!.GetValue<int>(),
                ["left_at"] = $"0x{close["LeftAt"]!.GetValue<uint>():X8}",
            }).ToArray());
        // RULE-HIRE-003, FND-HIRE-008: each drag or Reject press after the dump and hire_orders after it.
        if (trace["HireSteps"] is JsonArray hireSteps)
            run["hire_steps"] = new JsonArray(hireSteps.Select(step => (JsonNode)new JsonObject
            {
                ["slot"] = step!["Slot"]!.GetValue<int>(),
                ["sector"] = step["Sector"]!.GetValue<int>(),
                ["orders"] = new JsonArray(step["Orders"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
            }).ToArray());
        // RULE-UI-006, FND-UI-024: each gang-status marker drawing from the last full city redraw
        // before the dump on, as step, kind, player, sector and frame.
        if (trace["GangMarkers"] is JsonArray gangMarkers)
            run["gang_markers"] = new JsonArray(gangMarkers.Select(draw => (JsonNode)new JsonArray(
                draw!["Step"]!.GetValue<int>(), draw["Kind"]!.GetValue<int>(), draw["Player"]!.GetValue<int>(),
                draw["Sector"]!.GetValue<int>(), draw["Frame"]!.GetValue<int>())).ToArray());
        // RULE-UI-014, FND-UI-020: each key event the window procedure stored for a posted key, as
        // the virtual key, whether the Shift test reported Shift held, and the event's type,
        // character and key.
        if (trace["KeyEvents"] is JsonArray keyEvents)
            run["key_events"] = new JsonArray(keyEvents.Select(entry => (JsonNode)new JsonArray(
                entry!["VirtualKey"]!.GetValue<int>(), entry["Shift"]!.GetValue<bool>() ? 1 : 0,
                entry["Type"]!.GetValue<int>(), entry["Character"]!.GetValue<int>(), entry["Key"]!.GetValue<int>())).ToArray());
        // RULE-SETUP-009, FND-UI-022: each name typed into the setup name editor, with the name
        // record of its slot after OK.
        if (trace["NameEntries"] is JsonArray nameEntries)
            run["name_entries"] = new JsonArray(nameEntries.Select(entry => (JsonNode)new JsonObject
            {
                ["keys"] = entry!["Keys"]!.GetValue<string>(),
                ["slot"] = entry["Slot"]!.GetValue<int>(),
                ["name"] = Integers(entry["Name"]),
            }).ToArray());
        // RULE-TURN-005, SCR-UI-004: each order step after the dump, the popup it opened with its
        // items' commands and greyed states, the view, the card slots and the active player's orders.
        if (trace["OrderSteps"] is JsonArray orderSteps)
            run["order_steps"] = new JsonArray(orderSteps.Select(step =>
            {
                var probeStep = step!["Step"]!;
                var record = new JsonObject
                {
                    ["kind"] = probeStep["Kind"]!.GetValue<string>(),
                    ["target"] = probeStep["Target"]!.GetValue<int>(),
                    ["x"] = probeStep["X"]!.GetValue<int>(),
                    ["y"] = probeStep["Y"]!.GetValue<int>(),
                    ["choice"] = probeStep["Choice"]!.GetValue<int>(),
                    ["menu"] = step["Menu"]!.GetValue<int>(),
                };
                if (probeStep["Text"] is JsonNode text) record["text"] = text.GetValue<string>();
                if (step["Items"] is JsonArray items)
                    record["items"] = new JsonArray(items.Select(Integers).ToArray());
                record["city_view"] = step["CityView"]!.GetValue<bool>();
                if (step["Viewed"] is JsonNode viewed) record["viewed"] = viewed.GetValue<int>();
                record["cards"] = Integers(step["Cards"]);
                record["gangs"] = new JsonArray(step["Gangs"]!.AsArray().Select(Integers).ToArray());
                // A shot keeps its capture with the screens it is compared at.
                if (probeStep["Screens"] is JsonNode screens)
                {
                    record["screens"] = screens.GetValue<string>();
                    if (step["Shot"] is JsonNode shot
                        && CaptureFixture.ExtractShot(runDirectory, shot, screens.GetValue<string>()) is { } capture)
                        record["capture"] = capture;
                }
                return (JsonNode)record;
            }).ToArray());
        // RULE-SEARCH-001, FND-SEARCH-002: each click posted after the dump, whether the Search panel
        // was open after it, the active player and the whole filter table.
        if (trace["SearchClicks"] is JsonArray searchClicks)
            run["search_clicks"] = new JsonArray(searchClicks.Select(click => (JsonNode)new JsonObject
            {
                ["x"] = click!["X"]!.GetValue<int>(),
                ["y"] = click["Y"]!.GetValue<int>(),
                ["panel_open"] = click["PanelOpen"]!.GetValue<bool>(),
                ["active_player"] = click["ActivePlayer"]!.GetValue<int>(),
                ["filters"] = new JsonArray(click["Filters"]!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()),
            }).ToArray());
        // RULE-SETUP-008: each call of a planning entry panel, with the roll count and whether it
        // was shown, in the order of the calls.
        if (trace["Panels"] is JsonArray panelCalls)
        {
            var calls = new JsonArray();
            foreach (var call in panelCalls)
                calls.Add(new JsonObject
                {
                    ["panel"] = call!["Panel"]!.GetValue<string>(),
                    ["after_roll"] = call["AfterRoll"]!.GetValue<int>(),
                    ["shown"] = call["Shown"]!.GetValue<bool>(),
                });
            run["panels"] = calls;
        }
        // RULE-TIMER-002, RULE-TIMER-003: each timed planning turn the probe let run out, with
        // the limit, each bar redraw as elapsed milliseconds, width and effect slot, and the elapsed
        // milliseconds of the last test that did not end the turn and of the one that did.
        if (trace["Timers"] is JsonArray timerTurns)
        {
            var timers = new JsonArray();
            foreach (var timer in timerTurns)
            {
                var bars = new JsonArray();
                foreach (var bar in timer!["Bars"]!.AsArray())
                    bars.Add(new JsonArray(bar!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray()));
                timers.Add(new JsonObject
                {
                    ["turn"] = timer["Turn"]!.GetValue<int>(),
                    ["limit_ms"] = timer["LimitMs"]!.GetValue<int>(),
                    ["bars"] = bars,
                    ["last_unexpired_ms"] = timer["LastUnexpired"]!.GetValue<int>(),
                    ["expired_ms"] = timer["Expired"]!.GetValue<int>(),
                });
            }
            run["timers"] = timers;
        }
        // --capture: the drawing area at the dump, with a digest of each screen element's rectangle
        // (CaptureFixture).
        if (CaptureFixture.Extract(runDirectory, trace, screens) is { } capture)
            run["capture"] = capture;
        // --title-capture, --credits-capture, --setup-capture: the screens before the match.
        foreach (var (key, before) in CaptureFixture.ExtractBeforeMatch(runDirectory, trace))
            run[key] = before;
        // --setup-steps: the presses on the setup screen and the copies taken after them.
        if (CaptureFixture.ExtractSetupSteps(runDirectory, trace) is { } setupSteps)
            run["setup_steps"] = setupSteps;
        return run;
    }

    /// <summary>
    /// The arguments every extract command takes, <c>--experiment &lt;EXP-ID&gt; --out
    /// &lt;fixture.json&gt; &lt;run directory&gt;...</c> in that order, or null when they are missing.
    /// </summary>
    public static (string Experiment, string Output, string[] Runs)? ExtractArguments(string[] args)
    {
        var experiment = Option(args, "--experiment");
        var output = Option(args, "--out");
        var runs = args.Skip(5).ToArray();
        if (experiment is null || output is null || runs.Length == 0 || args[1] != "--experiment" || args[3] != "--out")
            return null;
        return (experiment, output, runs);
    }

    /// <summary>The value after <paramref name="name"/>, or null when it is absent.</summary>
    public static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    /// <summary>
    /// Writes a fixture: the header, the inputs that start a new game with the runs' setup choices,
    /// the experiment's own <paramref name="scriptInputs"/> after them, the seeds and the runs.
    /// </summary>
    public static void WriteFixture(string output, string experiment, string clock, string[] settings,
        IEnumerable<JsonObject> scriptInputs, JsonArray seeds, JsonArray runs)
    {
        var fixture = new JsonObject
        {
            ["experiment"] = experiment,
            ["build"] = "BLD-GOG-EN-1.1",
            ["starting_state"] = null,
            ["recording_xxh3"] = null,
            ["clock"] = clock,
            ["inputs"] = new JsonArray(
            [
                new JsonObject { ["tick"] = 0, ["name"] = "command", ["value"] = "File, New Game (0x8101)" },
                .. settings.Select(setting => new JsonObject { ["tick"] = 0, ["name"] = "setup", ["value"] = setting }),
                new JsonObject { ["tick"] = 0, ["name"] = "left_click", ["value"] = "Begin (416, 397)" },
                .. scriptInputs,
            ]),
            ["seeds"] = seeds,
            ["runs"] = runs,
        };
        File.WriteAllText(output, Serialize(fixture) + "\n");
    }

    /// <summary>The setup choices a run was recorded with, one line each; none for the defaults.</summary>
    public static string[] Settings(string runDirectory)
    {
        var trace = JsonSerializer.Deserialize<ProbeTrace>(File.ReadAllText(Path.Combine(runDirectory, "trace.json")))!;
        return (trace.Settings ?? NewGameSettings.Defaults).Describe().ToArray();
    }

    /// <summary>The orders and Done presses a run was recorded with, one input each.</summary>
    public static (string Name, string Value)[] Turns(string runDirectory)
    {
        var trace = JsonSerializer.Deserialize<ProbeTrace>(File.ReadAllText(Path.Combine(runDirectory, "trace.json")))!;
        return (trace.Settings ?? NewGameSettings.Defaults).DescribeTurns().ToArray();
    }

    private JsonArray EndState()
    {
        var rows = new JsonArray();
        Term(rows, "scenario", 0x004ABBE8, 1, 4);
        Term(rows, "mentality", 0x00487850, 1, 1);
        Term(rows, "turn_limit", 0x004A5EF8, 1, 4);
        Term(rows, "elapsed_turns", 0x0049CA68, 1, 4);
        Term(rows, "controller", 0x004AB638, 6, 4);
        Term(rows, "portrait", 0x004A5F00, 6, 1, signed: false);
        Term(rows, "player_active", 0x004ABBE0, 6, 1, signed: false);
        Term(rows, "reaction", 0x004AB650, 6, 4);
        Term(rows, "difficulty_band", 0x004A2570, 6, 4);
        Term(rows, "attitude", 0x004AB590, 36, 4);
        Term(rows, "cash", 0x004A25E8, 6, 4);
        Term(rows, "hq_sectors", 0x00494818, 6, 4);
        Term(rows, "hire_offers", 0x004ABBC0, 18, 1);
        Term(rows, "research_remaining", 0x004A2608, 384, 1);
        Term(rows, "crackdown_history", 0x004ABCC0, 128, 2);
        Term(rows, "modifier_right_hands", 0x004ABBD8, 6, 1, signed: false);
        Term(rows, "modifier_visibility", 0x004AB588, 6, 1, signed: false);
        Term(rows, "hire_force_modifier", 0x004A5EF0, 6, 1, signed: false);
        Term(rows, "modifier_elite", 0x004A2788, 6, 1, signed: false);
        Term(rows, "modifier_islands", 0x004ABC10, 6, 1, signed: false);
        Term(rows, "modifier_cash", 0x0049CA70, 6, 1, signed: false);
        Term(rows, "cash_earned", 0x004A27E0, 6, 4);
        Term(rows, "cash_spent", 0x0049CA78, 6, 4);
        Term(rows, "damage_inflicted", 0x004A5ED8, 6, 4);
        Term(rows, "casualties", 0x004AB620, 6, 4);
        Term(rows, "overthrow_count", 0x004A27A8, 6, 4);
        Term(rows, "hide_count", 0x004A25D0, 6, 4);
        Term(rows, "hire_role", 0x00482128, 6, 4);
        Term(rows, "previous_hire_role", 0x00482160, 6, 4);
        Term(rows, "scenario_score", 0x004A2790, 6, 4);
        Term(rows, "scenario_standing", 0x004ABC08, 6, 1, signed: false);
        // A run that ends the match stops at the endgame: its awards are given (RULE-AWARDS-001),
        // and only the first three entries of each player's list are written.
        if (ReadByte(0x004ABBD4, signed: false) != 0)
        {
            Term(rows, "match_over", 0x004ABBD4, 1, 1, signed: false);
            for (var slot = 0; slot < 6; slot++)
                for (var entry = 0; entry < 3; entry++)
                    rows.Add(new JsonObject { ["term"] = "player_awards", ["index"] = slot * 5 + entry,
                        ["value"] = BitConverter.ToInt32(_data, Offset(0x00494500 + (uint)(4 * (slot * 5 + entry)), 4)) });
        }

        string[] sectorFields =
        [
            "owner", "base_income", "base_tolerance", "cash_yield", "income", "tolerance", "support",
            "sites[0].definition", "sites[0].progress", "sites[1].definition", "sites[1].progress",
            "sites[2].definition", "sites[2].progress", "research_level", "factory", "crackdown_turns",
        ];
        string[] siteBonuses =
        [
            "site_combat", "site_defense", "site_stealth", "site_detect", "site_chaos", "site_control",
            "site_heal", "site_influence", "site_research", "site_strength", "site_blade", "site_ranged",
            "site_fighting", "site_martial_arts",
        ];
        for (var sector = 0; sector < 64; sector++)
        {
            var at = 0x004A08E8u + (uint)sector * 0x24;
            for (var i = 0; i < sectorFields.Length; i++)
                rows.Add(Field("FMT-STATE-002", sector, sectorFields[i], ReadByte(at + (uint)i, signed: i is not (13 or 14))));
            for (var i = 0; i < siteBonuses.Length; i++)
                rows.Add(Field("FMT-STATE-002", sector, siteBonuses[i], ReadByte(at + 0x16 + (uint)i, signed: true)));
        }

        string[] gangFields =
        [
            "player", "definition", "sector", "force", "weapon", "armor", "misc", "action", "target",
            "target_2", "repeat_action", "repeat_target", "visible_to[0]", "visible_to[1]", "visible_to[2]",
            "visible_to[3]", "visible_to[4]", "visible_to[5]", "combat", "defense", "stealth", "detect",
            "chaos", "control", "heal", "influence", "research", "strength", "blade", "ranged", "fighting",
            "martial_arts",
        ];
        for (var record = 0; record < 486; record++)
        {
            var at = 0x00498DA8u + (uint)record * 0x20;
            // FMT-STATE-001: sector 100 marks a slot with no living gang.
            if (ReadByte(at + 2, signed: true) == 100) continue;
            for (var i = 0; i < gangFields.Length; i++)
            {
                var unsigned = i is 1 or 7 or 10 || (i >= 12 && i < 18);
                rows.Add(Field("FMT-STATE-001", record, gangFields[i], ReadByte(at + (uint)i, signed: !unsigned)));
            }
        }

        // FMT-STATE-006: each player's Last Turn reports of the last resolution, 10-byte records at
        // 0x004AAE08 + player * 0x140, with the count at 0x004ABCA8 + player * 4.
        Term(rows, "last_turn_report_count", 0x004ABCA8, 6, 4);
        string[] reportFields = ["report_type", "arg1", "arg2", "arg3"];
        for (var player = 0; player < 6; player++)
        {
            var count = Math.Min(32, BitConverter.ToInt32(_data, Offset(0x004ABCA8u + (uint)player * 4, 4)));
            for (var index = 0; index < count; index++)
            {
                var at = 0x004AAE08u + (uint)(player * 0x140 + index * 10);
                for (var i = 0; i < reportFields.Length; i++)
                    rows.Add(Field("FMT-STATE-006", player * 32 + index, reportFields[i],
                        BitConverter.ToInt16(_data, Offset(at + 2 + (uint)i * 2, 2))));
            }
        }

        // FMT-STATE-003: the combat records, 10 bytes per player and roster slot at
        // 0x004A11E8 + (player * 81 + slot) * 10. A record no resolution has written, zero but its
        // police_damage of -1, is left out.
        string[] combatFields =
        [
            "definition", "force_start", "force_final", "force_shown", "damage_dealt", "retaliation_taken",
            "weapon", "armor", "misc", "police_damage",
        ];
        for (var record = 0; record < 486; record++)
        {
            var at = 0x004A11E8u + (uint)record * 10;
            if (_data.AsSpan(Offset(at, 9), 9).IndexOfAnyExcept((byte)0) < 0 && ReadByte(at + 9, signed: true) == -1)
                continue;
            for (var i = 0; i < combatFields.Length; i++)
                rows.Add(Field("FMT-STATE-003", record, combatFields[i], ReadByte(at + (uint)i, signed: i != 0)));
        }

        // FMT-STATE-008: the combat result rows, 0x96 bytes per sector at 0x004A8888 + sector * 0x96:
        // six entries of gang and target per player, then police_hit per player. Only the entries
        // that hold a gang and the police_hit values that are not 0 are written.
        for (var sector = 0; sector < 64; sector++)
        {
            var at = 0x004A8888u + (uint)sector * 0x96;
            for (var player = 0; player < 6; player++)
            {
                for (var k = 0; k < 6; k++)
                {
                    var entry = at + (uint)(player * 6 + k) * 4;
                    var gang = BitConverter.ToInt16(_data, Offset(entry, 2));
                    if (gang == -1) continue;
                    rows.Add(Field("FMT-STATE-008", sector, $"players[{player}][{k}].gang", gang));
                    rows.Add(Field("FMT-STATE-008", sector, $"players[{player}][{k}].target",
                        BitConverter.ToInt16(_data, Offset(entry + 2, 2))));
                }
                if (ReadByte(at + 0x90 + (uint)player, signed: false) is var hit and not 0)
                    rows.Add(Field("FMT-STATE-008", sector, $"police_hit[{player}]", hit));
            }
        }

        // FMT-STATE-007: the computer players' planning records, 16 bytes per player and roster slot
        // at 0x0048A250 + (player * 81 + slot) * 16. Only the fields that are not 0 are written, so a
        // record of zero bytes, which a player that has never planned keeps in every slot, has no row.
        string[] planningFields =
        [
            "family", "needs_family", "older_action", "older_target", "older_target_2", "previous_action",
            "previous_target", "previous_target_2", "planned_action", "planned_target", "planned_target_2",
            "unk_0B",
        ];
        for (var record = 0; record < 486; record++)
        {
            var at = 0x0048A250u + (uint)record * 0x10;
            for (var i = 0; i < planningFields.Length; i++)
            {
                var signed = planningFields[i] is "family" or "older_target" or "older_target_2" or "previous_target"
                    or "previous_target_2" or "planned_target" or "planned_target_2";
                if (ReadByte(at + (uint)i, signed) is var value and not 0)
                    rows.Add(Field("FMT-STATE-007", record, planningFields[i], value));
            }
            if (BitConverter.ToInt16(_data, Offset(at + 0x0C, 2)) is var weapon and not 0)
                rows.Add(Field("FMT-STATE-007", record, "weapon_cooldown", weapon));
            if (BitConverter.ToInt16(_data, Offset(at + 0x0E, 2)) is var armor and not 0)
                rows.Add(Field("FMT-STATE-007", record, "armor_cooldown", armor));
        }

        // The computer players' other planning state (FND-AI-019, FND-AI-044, FND-AI-045): ai_started at
        // 0x00482108, raider_mode at 0x00482158, placement_anchor at 0x0048E2F8, the two 16-bit values of
        // aux_records (14-byte records at 0x0048C0B0, focus at +0x0A and coverage_sector at +0x0C)
        // and sector_weight, the 16-bit value at +2 of the 14-byte records at
        // 0x0048E310 + player * 0x380 + sector * 14. An aux value or weight of 0 has no row.
        Term(rows, "ai_started", 0x00482108, 6, 1, signed: false);
        Term(rows, "raider_mode", 0x00482158, 6, 1, signed: false);
        Term(rows, "placement_anchor", 0x0048E2F8, 6, 4);
        for (var record = 0; record < 486; record++)
        {
            var at = 0x0048C0B0u + (uint)record * 14;
            if (BitConverter.ToInt16(_data, Offset(at + 0x0A, 2)) is var focus and not 0)
                rows.Add(new JsonObject { ["term"] = "aux_records.focus", ["index"] = record, ["value"] = focus });
            if (BitConverter.ToInt16(_data, Offset(at + 0x0C, 2)) is var coverage and not 0)
                rows.Add(new JsonObject { ["term"] = "aux_records.coverage_sector", ["index"] = record, ["value"] = coverage });
        }
        for (var player = 0; player < 6; player++)
            for (var sector = 0; sector < 64; sector++)
                if (BitConverter.ToInt16(_data, Offset(0x0048E310u + (uint)(player * 0x380 + sector * 14 + 2), 2)) is var weight and not 0)
                    rows.Add(new JsonObject { ["term"] = "sector_weight", ["index"] = player * 64 + sector, ["value"] = weight });

        return rows;
    }

    private void Term(JsonArray rows, string term, uint address, int count, int size, bool signed = true)
    {
        for (var index = 0; index < count; index++)
        {
            var at = address + (uint)(index * size);
            var value = size switch
            {
                1 => ReadByte(at, signed),
                2 => BitConverter.ToInt16(_data, Offset(at, 2)),
                _ => BitConverter.ToInt32(_data, Offset(at, 4)),
            };
            rows.Add(new JsonObject { ["term"] = term, ["index"] = index, ["value"] = value });
        }
    }

    private static JsonObject Field(string format, int record, string field, int value) =>
        new() { ["format"] = format, ["record"] = record, ["field"] = field, ["value"] = value };

    private int ReadByte(uint address, bool signed)
    {
        var value = _data[Offset(address, 1)];
        return signed ? (sbyte)value : value;
    }

    private int Offset(uint address, int size)
    {
        var offset = checked((int)(address - DataStart));
        if (offset < 0 || offset + size > _data.Length)
            throw new InvalidDataException($"0x{address:X8} lies outside the dumped .data section.");
        return offset;
    }

    /// <summary>Indented JSON with each element of a row list (a roll or a state row) on one line.</summary>
    public static string Serialize(JsonNode node, string indent = "")
    {
        if (node is JsonObject obj)
        {
            var inner = indent + "  ";
            var members = obj.Select(member =>
                $"{inner}{JsonSerializer.Serialize(member.Key)}: {Serialize(member.Value!, inner)}");
            return obj.Count == 0 ? "{}" : "{\n" + string.Join(",\n", members) + "\n" + indent + "}";
        }

        if (node is JsonArray array && array.Count > 0 && array.Any(item => item is JsonObject or JsonArray))
        {
            var inner = indent + "  ";
            var rows = array.Select(item => item is JsonObject nested && nested.Any(member => member.Value is JsonArray) ? inner + Serialize(item!, inner) : inner + item!.ToJsonString());
            return "[\n" + string.Join(",\n", rows) + "\n" + indent + "]";
        }

        return node?.ToJsonString() ?? "null";
    }

    private static JsonNode Integers(JsonNode? values) =>
        new JsonArray(values!.AsArray().Select(value => (JsonNode)value!.GetValue<int>()).ToArray());
}
