# research_remaining

How much research each player still needs for each item; 0 means the item is
researched. Any other value the game keeps: `INT8[384]`, element
`item * 6 + player`, at `0x004A2608`; every read loads it signed
[FND-RESEARCH-001, FND-RESEARCH-002, FND-PLATFORM-003, FND-STATE-004].
