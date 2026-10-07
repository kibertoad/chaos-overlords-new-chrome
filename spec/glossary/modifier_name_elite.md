# modifier_name_elite

The fixed upper-case string in the executable that a player's name must equal
to start with five extra equipped gangs. A string the game keeps, at
`0x00487BC0`; a match sets the flag at `0x004A2788 + player` [FND-SETUP-004,
FND-SETUP-015].
