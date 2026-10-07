# combat_advantage

Set when a computer player's gangs out-fight the visible defenders in more than
three quarters of another player's sectors. Any other value the game keeps:
the byte at +20 of a 24-byte player-pair record, element `observer * 6 +
other`, at `0x0048F810`, so the flag of the first pair is at `0x0048F824`
[FND-AI-018, FND-AI-032, FND-AI-044, FND-STATE-007, FND-STATE-011].
