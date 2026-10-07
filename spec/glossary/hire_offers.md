# hire_offers

The three gangs each player is offered for hire. Any other value the game
keeps: `INT8[18]`, element `player * 3 + offer slot`, at `0x004ABBC0`
[FND-HIRE-001, FND-HIRE-002, FND-AI-064]. An element holds a gang definition
number, -100 before the first offer, or the negated number of a gang just
hired or snubbed, which marks the slot to be refilled [FND-HIRE-001].
