# players_human

For each player slot, whether a human plays it. Any other value the game
keeps: `UINT8[6]` at `0x004ABC58`, set at setup to 1 where `controller` is 0
or 3 and to 0 otherwise, and set to 0 when a network player's slot is handed
to the computer [FND-COMLINK-007]. A save file keeps it as block 39
[FND-SAVE-001].
