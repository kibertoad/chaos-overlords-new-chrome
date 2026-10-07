# modifier_elite

Whether a player's name matched `modifier_name_elite`, which gives the player five extra equipped gangs. Any
other value the game keeps: `UINT8[6]`, indexed by player slot, at `0x004A2788`.
The new-match scan writes it for every slot in a local game and leaves it
alone in a network game [FND-SETUP-015].
