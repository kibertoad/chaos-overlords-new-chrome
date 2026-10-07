# combat_phase

The step of `resolution` in which each attacking gang makes its attack and
takes any retaliation, and at whose end the damage from attacks and police is
applied to Force. It runs after `chaos_phase` and before `transaction_phase`.
`police_phase` runs inside it, after the attacks and before the damage is
applied [FND-CHAOS-001, FND-COMBAT-001, FND-COMBAT-004, FND-GANG-003].
