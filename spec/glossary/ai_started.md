# ai_started

Whether a computer player's planning records have been reset for this match.
Any other value the game keeps: one byte per player slot at `0x00482108`,
cleared when a new match starts and set by the first planning pass or by the
takeover of a dropped network player [FND-AI-003, FND-AI-042, FND-AI-043,
FND-AI-045].
