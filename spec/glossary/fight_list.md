# fight_list

The gangs Detailed Combat shows against `combat_focal`, as element numbers:
`combat_focal` first, then its target, then every gang whose
`combat_results` entry in the sector targets it, then -2 for the police. A
list the game keeps, `INT16[36]`, at `0x00494780`, reset to -1 before each
build [FND-COMBAT-011].
