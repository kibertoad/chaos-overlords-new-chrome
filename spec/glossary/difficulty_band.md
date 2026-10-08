# difficulty_band

The per-player band, 0, 1 or 2, that adjusts several dice pools during
`resolution` according to the AI Mentality and whether the player is a
computer. Any other value the game keeps: `INT32LE[6]`, indexed by player
slot, at `0x004A2570` [FND-AI-007, FND-PLATFORM-003].
