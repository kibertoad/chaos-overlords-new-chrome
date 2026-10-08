# last_turn_reports

The reports of the last resolution, shown in the Last Turn Events panel. A
list the game keeps, 32 records of 10 bytes per player: element
`player * 32 + index`, at `0x004AAE08 + player * 0x140 + index * 10`, each
player's records in the order they were recorded [FND-EVENT-001]. Its
elements are FMT-STATE-006 [FND-EVENT-004].
