# DEV-UI-005

- Departs from: SCR-UI-003, SCR-UI-004, SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008
- Reason: Hover tooltips explain statistics, attributes, modifiers, modes, options and ranking
  scores, and a two-second rest on a command explains the order. A one-second rest on a sector of
  the city map, or on the sector view's owner strip or a cell of its nine-sector display, names
  the sector's owner and the owner's seat on the Overlord bar, and an arrow key that moves the
  selected sector names its owner on the message line (DEV-UI-023). The original shows a sector's
  owner only by the colour of its cell and of the owner strip.
- Setting: None
- Default: mandatory
- Justification: It adds information on hover and changes nothing else. The owner's name says in
  words what the colour already shows, for a player who cannot tell the overlord colours apart; a
  setting that hid it would only take information away. The owner tooltip waits for the pointer to
  rest and the line for a key, so a screen nobody points at or steers draws what the original
  draws.
- Tests: tests/Rechaos.Tests/SectorOwnerTextTests.cs
- Dropped: no
