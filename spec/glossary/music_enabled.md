# music_enabled

Whether music plays. Any other value the game keeps: a byte at `0x00487838`,
1 in the executable's data, cleared when `music_level` is set to 0 and set
again when it is set to any other level [FND-AUDIO-001, FND-AUDIO-007].
