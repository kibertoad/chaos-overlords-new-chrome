# planning_records

The computer players' per-gang planning state. A list the game keeps, one
16-byte record per player and roster slot, element `player * 81 +
roster_slot`, at `0x0048A250` [FND-AI-019, FND-AI-001]; its element format is
FMT-STATE-007. Fields: `family` (+0), `needs_family` (+1), `older_action`,
`older_target`, `older_target_2` (+2..+4), `previous_action`,
`previous_target`, `previous_target_2` (+5..+7), `planned_action`,
`planned_target`, `planned_target_2` (+8..+10), `unk_0B` (+11, never
addressed), `weapon_cooldown` (`INT16`, +12) and `armor_cooldown` (`INT16`,
+14) [FND-AI-019, FND-AI-021, FND-STATE-006, FND-AI-042].
