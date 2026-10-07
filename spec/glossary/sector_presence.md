# sector_presence

Which players had a gang in each sector when the current resolution began.
Any other value the game keeps: `UINT8[384]`, element `sector * 6 + player`,
kept as a local of the whole-turn resolver with no fixed address, element
`sector * 6 + player` at byte offset `sector * 6 + player` of the local
[FND-POLICE-002, FND-EVENT-004].
