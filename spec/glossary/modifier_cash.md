# modifier_cash

Whether a player's name matched `modifier_name_cash`, which gives the player $1,500 at the start. Any
other value the game keeps: `UINT8[6]`, indexed by player slot, at `0x0049CA70`.
The new-match scan writes it for every slot in a local game and leaves it
alone in a network game [FND-SETUP-015].
