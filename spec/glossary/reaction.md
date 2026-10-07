# reaction

A player's reaction value, drawn at new-game setup and not written again,
which scales how the computer players respond to attacks. Any other value the
game keeps: `INT32LE[6]`, indexed by player slot, at `0x004AB650`; the setup
draw stores 3 to 6 there, and save block 36 copies it [FND-AI-006,
FND-RNG-005, FND-RNG-006, FND-STATE-003].
