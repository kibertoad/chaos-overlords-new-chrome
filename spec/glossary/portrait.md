# portrait

The Overlord portrait a player slot shows, 0 to 14, which also chooses the
player's default name; 15 is an empty slot's image. Any other value the game
keeps: `UINT8[6]`, indexed by player slot, at `0x004A5F00` [FND-SETUP-002,
FND-SETUP-005, FND-SETUP-013]. The setup screens edit a copy at `0x00490678`,
which the setup reset fills with 0 for slot 0 and 15 for the others
[FND-SETUP-017].
