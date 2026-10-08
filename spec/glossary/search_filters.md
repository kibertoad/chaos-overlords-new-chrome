# search_filters

The Search panel's site selection: for each player and each of the 22 site
definitions, whether the city shows the uncontrolled sites of that definition.
Any other value the game keeps: `UINT8[132]`, element
`player * 22 + definition`, at `0x004A24E8` [FND-SEARCH-001, FND-SEARCH-003].
Each element is 0 or 1; the table is emptied when the match loop starts and is
not saved [FND-SEARCH-004, FND-COMLINK-006].
