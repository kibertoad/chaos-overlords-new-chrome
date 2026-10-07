# comlink_count

How many messages each player holds in `comlink_messages`, 0 to 16. Any other
value the game keeps: `INT32LE[6]`, at `0x004981E0 + player * 4`
[FND-COMLINK-001, FND-COMLINK-002]. Rules write `comlink_count[player]`.
