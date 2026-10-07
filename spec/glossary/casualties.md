# casualties

The number of each player's gangs that have died from damage, shown on the
endgame Stats panel as Casualties. It is raised by one for each gang whose
Force falls below 1 when the combat damage is applied, whether the damage came
from attacks or from the police; Terminate and the Eliminate clean-up do not
raise it. Any other value the game keeps: `INT32LE[6]`, indexed by player
slot, at `0x004AB620`, raised only at `0x00474889` and set to 0 at
`0x00476045` [FND-GANG-003, FND-GANG-005].
