# music_level

The Music volume the Options dialog shows, 0 to 10. Any other value the game
keeps: a DWORD at `0x00487868`, initialized to 5 and read from the registry
value `prefsVolumeCD` [FND-OPTIONS-001, FND-AUDIO-001].
