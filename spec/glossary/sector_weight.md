# sector_weight

A computer player's cached `visible_weight` of each sector, rebuilt at each
planning pass. Any other value the game keeps: the 16-bit value at +2 of the
14-byte per-sector record at `0x0048E310 + player * 0x380 + sector * 14`, not
saved [FND-AI-039, FND-AI-013, FND-AI-044].
