# phase_damage

The damage each gang has taken so far in the current combat phase, from
attacks, retaliations and the police, applied to Force at the end of the phase.
Any other value the game keeps: one integer per gang, element
`player * 81 + roster_slot` [FND-COMBAT-003]: a local 32-bit value of the
resolver, cleared at the start of `resolution` and capped at 10 before it is
applied [FND-COMBAT-008].
