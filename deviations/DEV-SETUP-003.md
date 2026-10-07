# DEV-SETUP-003

- Departs from: SCR-SETUP-003
- Reason: The player's name is edited on the card instead of in the Windows dialog. A press on
  the selected card's name band clears the name row and puts a caret in it; the row shows ten
  characters of the text, scrolled to keep the caret in view, drawn in the screen's font and
  green. The editor keeps the edit control's text, insertion point and selection as EXP-UI-051
  records them: characters come from the platform's keyboard layout with letters, accented
  Latin-1 letters included, in capitals; Left and Right move the caret one character from where
  it stands, Home and End to the ends, and without Shift clear the selection while with Shift
  they extend it; Backspace and Delete delete; a press on the row places the caret (with Shift it
  extends the selection) and dragging selects within the ten characters shown, where the control
  scrolls the text while the pointer is held past its edge; the text may run to 32,767
  characters; and Enter or a press elsewhere keeps the first ten characters as OK does while
  Escape or the right button leaves the name as Cancel does. The caret is a one-pixel line in the
  name's green that blinks every 530 ms, the default blink time of Windows, and shows at once
  when it moves, and a selection is drawn in black on green. There is no prompt, no OK or Cancel
  button, no Tab between controls, no clipboard, undo, double-click word selection or context
  menu. A press places the caret by the card's six-pixel cells, where the control places it by
  the dialog font's widths.
- Setting: None
- Default: mandatory
- Justification: The rebuild does not draw the look of the original's native Windows controls
  (owner decision, 2026-10-07): Windows draws the dialog, frame, buttons and caret in the style,
  colours and font of the Windows version it runs on, and the rebuild's screens draw no Windows
  elements (DEV-GFX-001, DEV-UI-019); only its reports of a failed start or a crash use a system
  message box. The game draws nothing while the dialog is open (EXP-UI-051), so the rest of the
  setup screen is the original's. The name typed, the way it is edited and the name OK keeps are
  those of the control, so a player can type and correct a name as before. The keys left out
  copy text between programs or undo one edit of a name ten characters long, and Tab only
  reaches buttons whose work Enter and Escape do.
- Tests: tests/Rechaos.Tests/SetupNameEditControlTests.cs,
  tests/Rechaos.Tests/SetupNameEditControlExperimentTests.cs, tests/Rechaos.Tests/ScreenCaptureTests.cs
- Dropped: no
