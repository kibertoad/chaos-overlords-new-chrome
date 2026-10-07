# retaliation_damage

The retaliation damage each attacking gang took in the current combat phase,
copied into `retaliation_taken` of its combat record. Any other value the game
keeps: a local 32-bit value per gang of the resolver, element
`player * 81 + roster_slot`, set to 0 and then written only for gangs whose
action is Attack [FND-COMBAT-008].
