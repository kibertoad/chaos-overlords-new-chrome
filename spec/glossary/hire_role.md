# hire_role

The hire role a computer player's schedule chose for its latest hire, 0 to 6.
Any other value the game keeps: `INT32LE[6]`, indexed by player slot, at
`0x00482128` [FND-AI-009, FND-AI-014]. A human player's entry holds -1 at the
end of every recorded run [EXP-TURN-016]; a player's first planning pass
writes 0 there before anything reads it [FND-AI-042].
