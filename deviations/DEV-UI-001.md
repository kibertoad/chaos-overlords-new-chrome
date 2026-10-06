# DEV-UI-001

- Departs from: RULE-UI-003, SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008, SCR-OPTIONS-001,
  SCR-GANG-001, SCR-GANG-002, SCR-FINANCE-001
- Reason: With Slide Panels on, a panel slides in as in the original but closes at once. The
  original's closing slide holds input for about a quarter of a second, and without it the close
  cue and the next cue start in the same frame, so the next cue cuts the close cue off.
- Setting: None
- Default: mandatory
- Justification: The closing slide holds input for about a quarter of a second and makes the next
  sound cue cut off the close cue. A panel's closing animation has no effect on play, and the
  opening slide is kept for a player who wants the motion.
- Dropped: no
