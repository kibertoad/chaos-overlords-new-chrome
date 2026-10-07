# planning_timed

Set while a human player plans under a time limit. Any other value the game
keeps: the byte at `0x00490698`, set when planning starts with a limit other
than -1 and cleared when planning ends [FND-TIMER-003].
