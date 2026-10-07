# combat_results

For each sector, which gangs of each player fought there in the last combat
phase, and whether the police attacked each player there. A list the game
keeps, of the combat result row (six players' rows of six four-byte entries,
then six police flag bytes, 150 bytes in all), 64 elements in ascending sector
order, at `0x004A8888 + sector * 0x96` [FND-AUDIO-002, FND-COMBAT-004]. An
entry is two `INT16LE` gang indices, `player * 81 + roster_slot`: the gang,
or -1 for an empty entry, and the gang's Attack target, or -1 when its action
was not Attack; only the first is cleared each phase. A gang is listed in its
own player's row of its own sector. The police flag of a player is set when
the police find one of its gangs there [FND-COMBAT-008, FND-COMBAT-011]. The rows are
written in `turn_order` and roster slot order [FND-COMBAT-004].
