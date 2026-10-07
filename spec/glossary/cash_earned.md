# cash_earned

The running total of cash a player has taken in, shown in the financial panel.
Any other value the game keeps: `INT32LE[6]`, indexed by player slot, at
`0x004A27E0` [FND-UPKEEP-001, FND-PLATFORM-003].
