# turn_limit

The match length in turns: 26, 52, 104 or 208 in a timed scenario (0 to 3),
52 when the setup screen opens, and 65535 in every other scenario, which each
match entry writes whatever length was chosen. Any other value the game keeps:
`INT32LE` at `0x004A5EF8` [SRC-MANUAL-GOG, FND-AI-005, FND-OBJECTIVE-003,
FND-SETUP-013, FND-SETUP-018].
