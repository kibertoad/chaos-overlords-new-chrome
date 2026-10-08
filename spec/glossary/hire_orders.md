# hire_orders

What each player has chosen to do with each offer this turn. Any other value
the game keeps: `INT8[18]`, element `player * 3 + offer slot`, at
`0x004A27C8` [FND-HIRE-001, FND-HIRE-002]. An element holds -1 for no order,
-2 to snub the offer, or the sector to place the hired gang in
[FND-HIRE-001].
