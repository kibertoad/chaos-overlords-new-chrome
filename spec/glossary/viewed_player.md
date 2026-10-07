# viewed_player

The player whose gangs the sector view shows and whose Overlord bar portrait
carries the active-player marker. It is `active_player` on the city screen;
the sector view sets it to the player whose portrait was pressed. Any other
value the game keeps: `INT32` at `0x00487B8C` [FND-UI-015, FND-UI-017,
FND-UI-018, FND-STATE-008].
