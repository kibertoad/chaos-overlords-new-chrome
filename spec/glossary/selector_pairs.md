# selector_pairs

The sector selector's list of score and sector pairs. A list the game keeps, of
64 records of an `INT32` score at +0 and an `INT32` sector at +4, at
`0x00489F50`, followed in memory by the selector's score table at `0x0048A150`
and by `planning_records`; only `select_sector` writes it, nothing clears it,
and it is not saved [FND-AI-066, FND-STATE-007].
