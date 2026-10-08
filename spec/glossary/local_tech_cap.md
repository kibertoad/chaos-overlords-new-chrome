# local_tech_cap

The Tech ceiling a computer gang's item choices apply (selector `0x62`): the
`tech_level` of the gang's definition, lowered to 5, 8 or 10 when the
`research_level` of the gang's sector is 0, 1 or 2, whoever owns the sector.
Any other value the game computes, element `player * 81 + roster_slot`
[FND-AI-024, FND-AI-054].
