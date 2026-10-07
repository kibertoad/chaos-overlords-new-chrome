# hire_force_modifier

Whether a player's name matched the name modifier that makes hires start at
Force 10. Any other value the game keeps: `UINT8[6]`, indexed by player slot,
at `0x004A5EF0`; nonzero when set. Set at new-game setup and kept in the save
[FND-HIRE-005].
