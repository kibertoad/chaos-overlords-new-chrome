# crackdown_history

The turns of the last two Crackdown occurrences in each sector. A list the
game keeps, one element per sector in ascending sector order, each element
`INT16LE[2]` holding a turn number or -100 for an empty slot, at
`0x004ABCC0 + sector * 4` [FND-POLICE-001, FND-PLATFORM-003]. The turn stored
and the window test both use `elapsed_turns` [FND-CHAOS-002].
