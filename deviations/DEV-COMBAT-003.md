# DEV-COMBAT-003

- Departs from: SCR-COMBAT-002
- Reason: The rebuild's Detailed Combat panel shows frame 0 of both strips from tick 0 to tick 2
  of every clip. In the original a paint of the window during those ticks copies the panel from
  its back buffer, whose apertures do not hold frame 0, so they show what was drawn there before
  the clip until tick 3.
- Setting: None
- Default: mandatory
- Justification: The paint comes from the host's windowing (another window uncovering the game, a
  focus change), not from the game or the player, and the clip player plainly means to show frame
  0 there, since it copies it to the screen at its setup. Reproducing it would mean modelling
  window paints and the back buffer's stale apertures for a glitch of at most half a second that
  changes nothing the player can do.
- Dropped: no
