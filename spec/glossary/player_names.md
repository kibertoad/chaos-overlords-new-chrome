# player_names

The players' names. Any other value the game keeps: six 12-byte records at
`0x004A2588 + player * 12` [FND-UI-003, FND-PLATFORM-003]. Byte 0 holds the
name's length, 1 to 10; the characters follow from byte 1, each from `0x20`
(space) to `0x5A` (`Z`), with a NUL after the last [FND-STATE-004].
