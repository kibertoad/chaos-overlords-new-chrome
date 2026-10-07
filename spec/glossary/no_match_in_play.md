# no_match_in_play

Set while no match is in play: from startup until a match starts, and again
once the end evaluation has finished a match, including the last planning
passes of the end sequence. Any other value the game keeps: the byte at
`0x004ABC9C` [FND-STATE-010].
