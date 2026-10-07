# scenario

The objective of the match. Any other value the game keeps: `INT32LE` at
`0x004ABBE8` [FND-RESEARCH-002, FND-SETUP-009, FND-PLATFORM-003]. 0 is Greed,
1 Power, 2 Acceptance, 3 Dominance, 4 Kill 'Em All, 5 Big 40, 6 Siege,
7 Eliminate, 8 Big Man and 9 Armageddon [FND-OBJECTIVE-003, FND-UI-033,
FND-TURN-003, FND-RESEARCH-002]. Saves and the network session transfer copy
the value unchanged [FND-OBJECTIVE-006].
