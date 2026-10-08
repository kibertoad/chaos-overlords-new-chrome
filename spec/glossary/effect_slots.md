# effect_slots

The loaded general sound effects, one per slot. A list the game keeps: 48
slots, of which 0 to 9 are used; slot 5 is loaded and emptied around each
Detailed Combat attack sound, and an empty slot plays nothing. The 48 records
of `0x114` bytes start at `0x00494C28`; each holds a loaded byte, the file's
path and the file loaded whole into memory [FND-AUDIO-002, FND-AUDIO-013,
FND-AUDIO-006]. Rules write
`effect_slots[slot]`.
