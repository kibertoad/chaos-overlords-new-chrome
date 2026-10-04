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
            // The fixture leaves out a record of zero bytes.
            var held = recorded.HasField("FMT-STATE-007", record, "family");
            for (var i = 0; i < fields.Length; i++)
            {
                var original = held ? recorded.Field("FMT-STATE-007", record, fields[i]) : 0;
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
                var original = held ? recorded.Field("FMT-STATE-007", record, name) : 0;
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
                Assert.True(recorded.Term("sector_weight", slot * 64 + sector) == planning.SectorWeight(player.Id, sector),
                    $"sector_weight of player {slot} at sector {sector}: the original holds {recorded.Term("sector_weight", slot * 64 + sector)}, the rebuild {planning.SectorWeight(player.Id, sector)}");
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
                if (recorded.Term("aux_records.focus", record) != planning.FocusValue(player.Id, gangSlot))
                    auxDifferences.Add($"aux record {record} (family {planning.Family(player.Id, gangSlot)}) focus: the original holds {recorded.Term("aux_records.focus", record)}, the rebuild {planning.FocusValue(player.Id, gangSlot)}");
                if (recorded.Term("aux_records.coverage_sector", record) != planning.CoverageSector(player.Id, gangSlot))
                    auxDifferences.Add($"aux record {record} (family {planning.Family(player.Id, gangSlot)}) coverage_sector: the original holds {recorded.Term("aux_records.coverage_sector", record)}, the rebuild {planning.CoverageSector(player.Id, gangSlot)}");
            }
        }
        Assert.True(auxDifferences.Count == 0, string.Join("; ", auxDifferences));
    }
}
