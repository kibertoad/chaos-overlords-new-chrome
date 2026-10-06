# modifier_name_cash

The fixed upper-case string in the executable that a player's name must equal
to start with $1,500. A string the game keeps, at `0x00487BD8`; a match sets
the flag at `0x0049CA70 + player` [FND-SETUP-001, FND-SETUP-015].
