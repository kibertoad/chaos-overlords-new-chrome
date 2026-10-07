# cash_spent

The running total of cash a player has paid out, shown in the financial panel
and used by the endgame awards. Any other value the game keeps: `INT32LE[6]`,
indexed by player slot, at `0x0049CA78` [FND-UPKEEP-001, FND-AWARDS-001,
FND-PLATFORM-003].
