# DEV-HELP-001

- Departs from: FMT-HELP-001, SCR-UI-009, RULE-HELP-001
- Replaces: RULE-HELP-001
- Reason: The rebuild shows the help topics in its own viewer, opened with F1 or from the menu.
  In the original, Help Topics does nothing and no key opens help: the call that would start the
  Windows help program on the help file is never reached (RULE-HELP-001). The viewer reads the
  player's own help file and approximates the help program's typography.
- Setting: None
- Default: mandatory
- Justification: The original ships a complete help file and a Help Topics item wired to nothing,
  which is logic that plainly does not do what it was written to do. Opening the help adds
  information and changes nothing the player can do or any rule's result. The help program the
  original would have started is no longer part of Windows, so a viewer of the rebuild's own is
  the only way to show the file, and a setting that brings back an inert menu item gives the
  player nothing.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs,
  tests/Rechaos.Tests/ExtractedHelpStoreTests.cs
- Dropped: no
