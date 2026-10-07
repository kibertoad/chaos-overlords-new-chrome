# comlink_cursor

The index, within the player's 16 elements of `comlink_messages`, of the
message Comlink View shows. Any other value the game keeps: `INT32LE[6]`, at
`0x004981C8 + player * 4` [FND-COMLINK-001, FND-COMLINK-004].
