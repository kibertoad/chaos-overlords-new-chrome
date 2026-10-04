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

    public static JsonObject ExtractRun(string runDirectory)
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
        return run;
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
}
