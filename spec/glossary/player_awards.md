# player_awards

The endgame awards each player has earned, as category numbers in the order
they were given: 0 Fist, 1 Skull, 2 Big Fat Chicken, 3 Dollar Sign, 4 Safe.
A table the game keeps: `INT32LE[5]` per player slot at `0x00494500 + 20 *
slot`, storing the category numbers above; only the first three entries are
cleared, to -1, before the awards are given [FND-AWARDS-001, FND-AWARDS-004].
