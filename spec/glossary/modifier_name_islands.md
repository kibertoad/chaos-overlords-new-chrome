# modifier_name_islands

The fixed upper-case string in the executable that a player's name must equal
to give every unowned sector a permanent Crackdown. A string the game keeps,
at `0x00487BCC`; a match sets the flag at `0x004ABC10 + player`
[FND-SETUP-003, FND-SETUP-015].
