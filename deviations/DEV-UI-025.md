# DEV-UI-025

- Departs from: RULE-UI-004
- Reason: Where a number cell's source column lies wholly or partly outside the glyph sheet's
  bitmap, the original changes only the pixels whose source lies inside and leaves the rest of
  the cell holding whatever an earlier draw left on the screen, a glyph of an earlier value
  included (EXP-UI-002). The rebuild draws the same inside pixels, and the rest of the cell shows
  what the rebuild drew beneath it in the same frame: the panel or console background.
- Setting: None
- Default: mandatory
- Justification: The rebuild draws every frame afresh from the match state and keeps no screen
  between frames, so there is no earlier draw for the cell to keep. Only a value whose first
  quotient is 69 or more, or -2147483648, reaches it, and what the original then shows depends on
  the order in which earlier values were drawn, which no rule or strategy uses. Where nothing was drawn before,
  as at a panel's first draw, the two agree pixel for pixel (EXP-UI-027, EXP-UI-028).
- Tests: tests/Rechaos.Tests/ItemInformationLayoutTests.cs
- Dropped: no
