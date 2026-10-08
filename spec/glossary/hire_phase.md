# hire_phase

The step of `resolution` that carries out hires and snubs, player by player
and offer slot by offer slot. It runs after `control_phase` and before
`turn_end`: the Control loop's exit jumps to its first instruction
(`0x00475862`), and its own exit to the presence countdown (`0x00475E16`)
[FND-EQUIP-006, FND-TURN-008].
