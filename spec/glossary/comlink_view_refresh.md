# comlink_view_refresh

Set by the Comlink recorder when it stores a message for the active player
while `comlink_view_open` is set; the View panel clears it and redraws the
message on show with the new count. Any other value the game keeps: `UINT8` at
`0x004877D0` [FND-COMLINK-009].
