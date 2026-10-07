# comlink_messages

The messages in each player's Comlink inbox. A list the game keeps, of
FMT-STATE-005, 16 elements per player at `0x0049CA90 + player * 0xA60`, kept
in the order they arrived, oldest first. When all 16 are in use, a new message
drops the oldest and the rest move down [FND-COMLINK-001, FND-COMLINK-004].
Emptied when the match loop starts, and cut at the front of its read messages
when its player finishes planning (RULE-COMLINK-007) [FND-COMLINK-006].
