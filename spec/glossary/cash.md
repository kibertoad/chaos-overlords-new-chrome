# cash

A player's money, shown as Cash. Any other value the game keeps:
`INT32LE[6]`, indexed by player slot, at `0x004A25E8` [FND-EQUIP-006,
FND-UPKEEP-001, FND-PLATFORM-003]. Rules write `cash[player]`.
