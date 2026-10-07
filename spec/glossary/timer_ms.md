# timer_ms

The Windows system time in milliseconds. A value from outside the game:
`UINT32`, read from the multimedia function `timeGetTime`; it changes every
millisecond. The game reads it when the process starts, to seed `rng`
[FND-RNG-001], and the planning timer reads it at the start of planning and at
every check (RULE-TIMER-002, RULE-TIMER-003).
