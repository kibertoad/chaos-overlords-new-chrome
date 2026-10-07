# overthrow_count

The number of sectors each player has taken from another player, used by the
endgame awards. The Control pass raises the winner's entry by one when it
takes a sector that had an owner, at `0x00475753`; taking a neutral sector does
not count. Any other value the game keeps: `INT32LE[6]`, indexed by player
slot, at `0x004A27A8` [FND-AWARDS-001, FND-PLATFORM-003, FND-CONTROL-003].
