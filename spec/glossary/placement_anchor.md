# placement_anchor

The sector a computer player places its new gangs in, stored as the sector
plus `0x40`. Any other value the game keeps: `INT32LE[6]`, one per player
slot, at `0x0048E2F8` [FND-AI-010, FND-AI-051].
