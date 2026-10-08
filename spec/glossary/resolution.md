# resolution

The part of a turn that carries out every player's orders, after
`planning_phase`. Its steps run in this order: `instant_phase`,
`chaos_phase`, `combat_phase` (with `police_phase` inside it),
`transaction_phase`, `chaos_payout_phase`, `terminate_phase`, `move_phase`,
`control_phase`, `hire_phase` and `turn_end` [FND-CHAOS-001,
FND-COMBAT-001, FND-MOVE-001, FND-EQUIP-006, FND-TURN-003, FND-TURN-008].
Before `instant_phase` it clears each player's report count and notes where
each player has gangs, player by player, and then moves each sector's
Tolerance one step toward normal [FND-TURN-008]. Just before resolution
starts, the previous turn's Last Turn reports are cleared [FND-EVENT-001].
