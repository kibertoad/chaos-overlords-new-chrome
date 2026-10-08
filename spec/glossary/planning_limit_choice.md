# planning_limit_choice

The planning time limit chosen on the setup screen: 0 none, 1 thirty seconds,
2 two minutes, 3 five minutes. Any other value the game keeps: a DWORD at
`0x00487854`, initialized to 0 and read from the registry value
`prefsTimeLimit` [FND-OPTIONS-001, FND-TIMER-001].
