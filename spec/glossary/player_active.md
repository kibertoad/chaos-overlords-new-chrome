# player_active

Whether a player is still in the match. Any other value the game keeps:
`UINT8[6]`, indexed by player slot, at `0x004ABBE0` [FND-TURN-003,
FND-OBJECTIVE-003, FND-STATE-004]. It is set for all six slots when a new
match starts and saved with the match [FND-STATE-004]. It is cleared only near
the end of `resolution`, for a player who owns no sector and has no gang
[FND-TURN-003, FND-STATE-004].
