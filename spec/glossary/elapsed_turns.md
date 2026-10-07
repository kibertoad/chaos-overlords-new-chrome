# elapsed_turns

The number of turns completed, counted from 0. Any other value the game keeps:
`INT32LE` at `0x0049CA68` [FND-AI-009, FND-PLATFORM-003]. A new match sets it
to 0; it goes up by 1 after each `resolution`, before the next `turn_start`,
so it is 0 throughout the first turn. The Crackdown window and history read
it [FND-TURN-006].
