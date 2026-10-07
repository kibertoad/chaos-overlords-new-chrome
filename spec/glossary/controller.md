# controller

Who plays each player slot. Any other value the game keeps: `INT32LE[6]`,
indexed by player slot, at `0x004AB638` [FND-SETUP-002, FND-PLATFORM-003].
The values are -1 for an empty setup slot, 0 for a human at this computer, 1
for a computer player and 3 for a human playing over the network
[FND-SETUP-002, FND-AI-004, FND-TURN-005]. A local human who is eliminated
becomes -2 at the start of the next round and -1 once the elimination card
has been shown, so a retired player is an empty slot [FND-OBJECTIVE-004].
