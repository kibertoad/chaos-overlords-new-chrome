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
        for (var record = 0; record < 6 * AiPlanningState.GangSlotsPerPlayer; record++)
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
                Assert.True(original == rebuilt,
                    $"planning record {record} {fields[i]}: the original holds {original}, the rebuild {rebuilt}");
            }
            (string Name, int Offset)[] cooldowns = [("weapon_cooldown", 12), ("armor_cooldown", 14)];
            foreach (var (name, offset) in cooldowns)
            {
                var original = recorded.FieldOrZero("FMT-STATE-007", record, name);
                var rebuilt = BitConverter.ToInt16(image, at + offset);
                Assert.True(original == rebuilt,
                    $"planning record {record} {name}: the original holds {original}, the rebuild {rebuilt}");
            }
        }

        var auxDifferences = new List<string>();
        foreach (var player in match.Players)
        {
            var slot = player.Id.Value;
            Assert.True(recorded.Term("ai_started", slot) != 0 == planning.HasPlanned(player.Id),
                $"ai_started of player {slot}: the original holds {recorded.Term("ai_started", slot)}");
            Assert.True(recorded.Term("raider_mode", slot) != 0 == planning.RaiderMode(player.Id),
                $"raider_mode of player {slot}: the original holds {recorded.Term("raider_mode", slot)}");
            Assert.True(recorded.Term("placement_anchor", slot) == planning.SectorAnchor(player.Id),
                $"placement_anchor of player {slot}: the original holds {recorded.Term("placement_anchor", slot)}, the rebuild {planning.SectorAnchor(player.Id)}");
            for (var sector = 0; sector < 64; sector++)
                Assert.True(recorded.TermOrZero("sector_weight", slot * 64 + sector) == planning.SectorWeight(player.Id, sector),
                    $"sector_weight of player {slot} at sector {sector}: the original holds {recorded.TermOrZero("sector_weight", slot * 64 + sector)}, the rebuild {planning.SectorWeight(player.Id, sector)}");
            // FND-AI-044: the original writes an aux record only when a planning pass finds the slot
            // flagged for a family, and every reader reads a computer player's active gang after
            // that. The rebuild starts every record at -1 or the gang's sector where the original
            // holds 0, which no reader sees, so only the active gangs of computer players whose
            // flag a pass has handled are compared; a gang hired since the last pass is still
            // flagged.
            if (player.Setup.Controller == PlayerController.Human) continue;
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

    // FMT-STATE-003, RULE-COMBAT-002, RULE-POLICE-001: the combat records the last resolution wrote,
    // rebuilt from its attack and police events. The original writes bytes 0 to 8 only for the gangs
    // that fought and keeps the bytes of earlier resolutions in the others, so only the records of
    // gangs that fought are compared, and police_damage in all 486. force_shown is Detailed
    // Combat's (RULE-COMBAT-004), which the probe switches off, and damage_dealt and
    // retaliation_taken of a gang that did not attack are undefined (FND-COMBAT-008).
    private static void AssertCombatRecordsMatch(RecordedRun recorded, MatchState match)
    {
        var turn = match.Outcome?.Turn ?? match.Coordinator.Turn - 1;
        var fought = new Dictionary<int, (CombatantDetails Gang, int Damage)>();
        var attacks = new Dictionary<int, (int Dealt, int Taken)>();
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
                    (evaded ? -1 : fight.Damage, fight.RetaliationDamage);
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
        Assert.True(differences.Count == 0, string.Join("; ", differences));
    }
}
