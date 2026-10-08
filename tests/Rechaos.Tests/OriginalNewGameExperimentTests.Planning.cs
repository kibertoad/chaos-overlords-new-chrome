using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    // FMT-STATE-007, RULE-AI-001, RULE-AI-002, RULE-AI-003, RULE-AI-019 to RULE-AI-031: the computer
    // players' planning records as the original holds them in memory, byte for byte, with the
    // first-pass and raider flags, the placement anchors (RULE-AI-010), the two 16-bit values of
    // each aux record and the sector weights the last pass cached.
    private static void AssertPlanningStateMatches(RecordedRun recorded, MatchState match)
    {
        var planning = match.AiPlanning;
        var image = planning.PlanningRecordImage();
        string[] fields =
        [
            "family", "needs_family", "older_action", "older_target", "older_target_2", "previous_action",
            "previous_target", "previous_target_2", "planned_action", "planned_target", "planned_target_2",
            "unk_0B",
        ];
        // The message is built only for a value that differs.
        static void Expect(int original, int rebuilt, string what)
        {
            if (original != rebuilt) Assert.Fail($"{what}: the original holds {original}, the rebuild {rebuilt}");
        }

        for (var record = 0; record < MatchLimits.PlayerCount * AiPlanningState.GangSlotsPerPlayer; record++)
        {
            var at = record * AiPlanningState.PlanningRecordSize;
            // The fixture leaves out every field that holds 0.
            for (var i = 0; i < fields.Length; i++)
            {
                var original = recorded.FieldOrZero("FMT-STATE-007", record, fields[i]);
                var rebuilt = fields[i] is "family" or "older_target" or "older_target_2" or "previous_target"
                    or "previous_target_2" or "planned_target" or "planned_target_2"
                    ? (int)(sbyte)image[at + i]
                    : image[at + i];
                if (original != rebuilt) Expect(original, rebuilt, $"planning record {record} {fields[i]}");
            }
            (string Name, int Offset)[] cooldowns = [("weapon_cooldown", 12), ("armor_cooldown", 14)];
            foreach (var (name, offset) in cooldowns)
            {
                var original = recorded.FieldOrZero("FMT-STATE-007", record, name);
                var rebuilt = BitConverter.ToInt16(image, at + offset);
                if (original != rebuilt) Expect(original, rebuilt, $"planning record {record} {name}");
            }
        }

        var auxDifferences = new List<string>();
        foreach (var player in match.Players)
        {
            var slot = player.Id.Value;
            Expect(recorded.Term("ai_started", slot) != 0 ? 1 : 0, planning.HasPlanned(player.Id) ? 1 : 0,
                $"ai_started of player {slot} (as 0 or 1)");
            Expect(recorded.Term("raider_mode", slot) != 0 ? 1 : 0, planning.RaiderMode(player.Id) ? 1 : 0,
                $"raider_mode of player {slot} (as 0 or 1)");
            Expect(recorded.Term("placement_anchor", slot), planning.SectorAnchor(player.Id),
                $"placement_anchor of player {slot}");
            for (var sector = 0; sector < MatchLimits.SectorCount; sector++)
            {
                var original = recorded.TermOrZero("sector_weight", slot * MatchLimits.SectorCount + sector);
                var rebuilt = planning.SectorWeight(player.Id, sector);
                if (original != rebuilt) Expect(original, rebuilt, $"sector_weight of player {slot} at sector {sector}");
            }
            // FND-AI-081: the original writes an aux record only when a planning pass finds the slot
            // flagged for a family, and every reader reads a computer player's active gang after
            // that. The rebuild starts every record at -1 or the gang's sector where the original
            // holds 0, which no reader sees, so only the active gangs of computer players whose
            // flag a pass has handled are compared; a gang hired since the last pass is still
            // flagged. Before a player's first pass (ai_started 0, as at the first planning entry of
            // EXP-UI-001) no pass has written any of its records.
            if (player.Setup.Controller == PlayerController.Human || !planning.HasPlanned(player.Id)) continue;
            for (var gangSlot = 0; gangSlot < player.Gangs.Count; gangSlot++)
            {
                if (!player.Gangs[gangSlot].IsActive || planning.NeedsFamily(player.Id, gangSlot)) continue;
                var record = slot * AiPlanningState.GangSlotsPerPlayer + gangSlot;
                if (recorded.TermOrZero("aux_records.focus", record) != planning.FocusValue(player.Id, gangSlot))
                    auxDifferences.Add($"aux record {record} (family {planning.Family(player.Id, gangSlot)}) focus: the original holds {recorded.TermOrZero("aux_records.focus", record)}, the rebuild {planning.FocusValue(player.Id, gangSlot)}");
                if (recorded.TermOrZero("aux_records.coverage_sector", record) != planning.CoverageSector(player.Id, gangSlot))
                    auxDifferences.Add($"aux record {record} (family {planning.Family(player.Id, gangSlot)}) coverage_sector: the original holds {recorded.TermOrZero("aux_records.coverage_sector", record)}, the rebuild {planning.CoverageSector(player.Id, gangSlot)}");
            }
        }
        Assert.True(auxDifferences.Count == 0, string.Join("; ", auxDifferences));
    }

    // FMT-STATE-003, FMT-STATE-008, RULE-COMBAT-002, RULE-POLICE-001: the combat records and result
    // rows the last resolution wrote, rebuilt from its attack and police events. The original writes
    // bytes 0 to 8 of a record only for the gangs that fought and keeps the bytes of earlier
    // resolutions in the others, so only the records of gangs that fought are compared, and
    // police_damage in all 486. force_shown is Detailed Combat's (RULE-COMBAT-004), which the probe
    // switches off, and damage_dealt and retaliation_taken of a gang that did not attack are
    // undefined (FND-COMBAT-008). Every result row is compared.
    private static void AssertLastCombatMatches(RecordedRun recorded, MatchState match)
    {
        var turn = match.Outcome?.Turn ?? match.Coordinator.Turn - 1;
        var fought = new Dictionary<int, (CombatantDetails Gang, int Damage)>();
        var attacks = new Dictionary<int, (int Dealt, int Taken, int Target)>();
        var police = new Dictionary<int, int>();
        void Fought(CombatantDetails gang, int damage)
        {
            var record = gang.Owner.Value * AiPlanningState.GangSlotsPerPlayer + gang.RosterSlot!.Value;
            fought[record] = (gang, (fought.TryGetValue(record, out var known) ? known.Damage : 0) + damage);
        }
        foreach (var gameEvent in match.Events.Where(gameEvent => gameEvent.Turn == turn))
        {
            if (gameEvent is { Kind: GameEventKind.PoliceAttackResolved, PoliceAttack: { Detected: true, Target: { } target } found })
            {
                Fought(target, found.Successes);
                police[target.Owner.Value * AiPlanningState.GangSlotsPerPlayer + target.RosterSlot!.Value] = found.Successes;
            }
            else if (gameEvent is { Action: GangAction.Attack, Resolution: { Attacker: { } attacker, Defender: { } defender } fight })
            {
                var evaded = fight.Code == CommandResolutionCode.TargetEvaded;
                Fought(attacker, fight.RetaliationDamage);
                Fought(defender, evaded ? 0 : fight.Damage);
                attacks[attacker.Owner.Value * AiPlanningState.GangSlotsPerPlayer + attacker.RosterSlot!.Value] =
                    (evaded ? -1 : fight.Damage, fight.RetaliationDamage,
                        defender.Owner.Value * AiPlanningState.GangSlotsPerPlayer + defender.RosterSlot!.Value);
            }
        }

        var differences = new List<string>();
        void Compare(int record, string field, int rebuilt)
        {
            var original = recorded.HasField("FMT-STATE-003", record, field) ? recorded.Field("FMT-STATE-003", record, field) : (int?)null;
            if (original != rebuilt)
                differences.Add($"combat record {record} {field}: the original holds {original?.ToString() ?? "nothing"}, the rebuild {rebuilt}");
        }
        for (var record = 0; record < 6 * AiPlanningState.GangSlotsPerPlayer; record++)
        {
            var original = recorded.HasField("FMT-STATE-003", record, "police_damage")
                ? recorded.Field("FMT-STATE-003", record, "police_damage")
                : -1;
            var rebuilt = police.GetValueOrDefault(record, -1);
            if (original != rebuilt)
                differences.Add($"combat record {record} police_damage: the original holds {original}, the rebuild {rebuilt}");
        }
        foreach (var (record, (gang, damage)) in fought)
        {
            Compare(record, "definition", gang.DefinitionId);
            Compare(record, "force_start", gang.Force!.Value);
            Compare(record, "force_final", gang.Force!.Value - Math.Min(damage, 10));
            Compare(record, "weapon", gang.WeaponItemId ?? -1);
            Compare(record, "armor", gang.ArmorItemId ?? -1);
            Compare(record, "misc", gang.MiscellaneousItemId ?? -1);
            if (!attacks.TryGetValue(record, out var attack)) continue;
            Compare(record, "damage_dealt", attack.Dealt);
            Compare(record, "retaliation_taken", attack.Taken);
        }

        // FMT-STATE-008, RULE-COMBAT-002: the result row of each sector lists, per player, the gangs
        // that fought there in roster order, with the target of each attacker, and marks the players
        // the police found there. A row the phase left empty is not in the fixture.
        for (var sector = 0; sector < 64; sector++)
            for (var player = 0; player < 6; player++)
            {
                var gangs = fought
                    .Where(entry => entry.Key / AiPlanningState.GangSlotsPerPlayer == player && entry.Value.Gang.SectorId == sector)
                    .Select(entry => entry.Key).Order().ToArray();
                for (var k = 0; k < 6; k++)
                {
                    var gang = k < gangs.Length ? gangs[k] : -1;
                    var target = gang != -1 && attacks.TryGetValue(gang, out var attack) ? attack.Target : -1;
                    (string Field, int Rebuilt)[] entry = [($"players[{player}][{k}].gang", gang), ($"players[{player}][{k}].target", target)];
                    foreach (var (field, rebuilt) in entry)
                    {
                        var original = recorded.HasField("FMT-STATE-008", sector, $"players[{player}][{k}].gang")
                            ? recorded.Field("FMT-STATE-008", sector, field)
                            : -1;
                        if (original != rebuilt)
                            differences.Add($"combat result row {sector} {field}: the original holds {original}, the rebuild {rebuilt}");
                    }
                }
                var hit = recorded.FieldOrZero("FMT-STATE-008", sector, $"police_hit[{player}]");
                var found = police.Keys.Any(record => record / AiPlanningState.GangSlotsPerPlayer == player && fought[record].Gang.SectorId == sector) ? 1 : 0;
                if (hit != found)
                    differences.Add($"combat result row {sector} police_hit[{player}]: the original holds {hit}, the rebuild {found}");
            }
        Assert.True(differences.Count == 0, string.Join("; ", differences));
    }
}
