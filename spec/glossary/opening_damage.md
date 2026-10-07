# opening_damage

The damage each gang's own attack did in the current combat phase, or -1 when
the target evaded, copied into `damage_dealt` of the gang's combat record. Any
other value the game keeps: a local 32-bit value per gang of the resolver,
element `player * 81 + roster_slot`, set only for gangs whose action is Attack
[FND-COMBAT-008].
