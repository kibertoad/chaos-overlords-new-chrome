# aux_records

A second per-gang record of the computer players. A list the game keeps, of
14-byte records, element `player * 81 + roster_slot`, at `0x0048C0B0`, with
the two values the rules use at +0x0A and +0x0C [FND-AI-015, FND-AI-081,
FND-STATE-007]. Fields the rules use: `focus`, the first 16-bit
value, whose meaning depends on the family, and `coverage_sector`, the second
16-bit value, the sector a family-6 gang heads for or covers [FND-AI-015,
FND-AI-013].
