# scenario_standing

For each active player, the number of players with a strictly greater
`scenario_score`; 0xFF for an inactive player. Any other value the game
keeps: `UINT8[6]`, indexed by player slot, at `0x004ABC08` [FND-AI-005,
FND-AI-009]. Selector `0x2D` of the computer players searches these bytes for
player slot numbers, as if the table listed players in ranking order
[FND-STATE-004].
