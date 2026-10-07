# combat_focal_bar

The Force bar of `combat_focal` in the current clip. Any other value the game
keeps: an `INT16[4]` rectangle (top, left, bottom, right) at `0x00494768`,
top 247, bottom 250 and right end `256 + 6 * force_shown` [FND-COMBAT-011].
