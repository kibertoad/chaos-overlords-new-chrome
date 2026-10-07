# planning_limit_ms

The planning time limit in milliseconds, -1 for none. Any other value the game
keeps: an `INT32` at `0x0049069C`, 0 in the executable's data and set at each
entry into a match [FND-TIMER-001, FND-TIMER-003].
