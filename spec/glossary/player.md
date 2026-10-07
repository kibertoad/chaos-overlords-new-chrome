# player

A player slot, 0 to 5. The game keeps no player structure: each per-player
value is an array of its own indexed by the slot, such as `cash`,
`controller` and `hire_offers`, and each player's gangs are the 81 elements of
`gangs` from `player * 81` [FND-HIRE-002, FND-PLATFORM-003].
