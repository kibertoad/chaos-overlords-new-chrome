# fight_marks

Whether each gang took part in a gang fight in the current combat phase. Any
other value the game keeps: one value per gang, element
`player * 81 + roster_slot`, written by the attack block and never read as a
condition for an attack [FND-COMBAT-006]: a local byte of the resolver, set
for every attacker, every attack's target and every gang the police find, and
copied at the record fill into the `UINT8[486]` at `0x00498BC0`
[FND-COMBAT-008].
