# modifier_right_hands

Whether a player's name matched `modifier_name_right_hands`, which gives the player five extra unequipped gangs. Any
other value the game keeps: `UINT8[6]`, indexed by player slot, at `0x004ABBD8`.
The new-match scan writes it for every slot in a local game and leaves it
alone in a network game [FND-SETUP-015].
