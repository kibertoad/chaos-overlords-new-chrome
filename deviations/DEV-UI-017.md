# DEV-UI-017

- Departs from: RULE-UI-014
- Reason: Closing the window ends the program after the rolling autosave is written. The original
  treats a close as File, Exit and offers to save during a game.
- Setting: None
- Default: mandatory
- Justification: The rolling autosave keeps the match as it stood at the start of the turn, and
  orders given since then can be saved from the Escape menu before closing. The rebuild's saves go
  to named slots, so the original's save dialog has no counterpart to offer.
- Dropped: 2026-10-06, the rebuild now asks as the original does (RULE-UI-015): closing the window
  or quitting to the title while the match changed since it was last saved or loaded offers to save
  first, cancel, or leave without saving. Asking only when something would be lost costs a player
  nothing, and the autosave holds only the start of the turn. The autosave is still written on the
  way out.
