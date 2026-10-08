# last_turn_report_count

How many reports each player has in `last_turn_reports`. Any other value the
game keeps: `INT32LE[6]` at `0x004ABCA8 + player * 4` [FND-EVENT-001,
FND-EVENT-004]. Rules write
`last_turn_report_count[player]`.
