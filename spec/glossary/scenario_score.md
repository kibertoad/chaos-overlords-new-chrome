# scenario_score

Each player's score toward the scenario's objective, rebuilt when a match
starts and by the end evaluation, and read as stored between them. Any other value the game keeps: `INT32LE[6]`, indexed by player
slot, at `0x004A2790` [FND-AI-005, FND-TURN-003, FND-PLATFORM-003].
