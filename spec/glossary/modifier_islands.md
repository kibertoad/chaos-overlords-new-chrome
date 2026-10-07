# modifier_islands

Whether a player's name matched `modifier_name_islands`, which puts every neutral sector under a permanent Crackdown. Any
other value the game keeps: `UINT8[6]`, indexed by player slot, at `0x004ABC10`.
The new-match scan writes it for every slot in a local game and leaves it
alone in a network game [FND-SETUP-015].
