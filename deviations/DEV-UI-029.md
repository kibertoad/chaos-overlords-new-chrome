# DEV-UI-029

- Departs from: SCR-UI-004, SCR-UI-009, SCR-SETUP-001, RULE-UI-015
- Reason: The rebuild draws none of the Windows controls the original uses. Where the original
  hands a choice to a control Windows draws (the menu bar, popup menus, the common file dialogs,
  dialog boxes with their edit controls, list boxes, combo boxes and buttons, and message boxes),
  the rebuild draws a control of its own inside the 640-by-460 drawing area, in the style of the
  game's screens. Each replacement offers the choices the original's control offers and leads to
  the same result; where a replacement also changes what the player does, an entry of its own
  records that, and the table below names it. Two things stay the system's: the frame and title
  bar of the rebuild's window when it is not full screen, and the dialogs the rebuild opens before
  or outside its own drawing: the report that it could not start or stopped unexpectedly, which
  stands where the original shows dialogs 132 and 135 (RULE-UI-013) and is shown when the game's
  own drawing may not be working, and, on macOS and Linux, the question that asks for the original
  installation when a start finds no current asset pack. Windows shows the report in a message
  box; macOS and Linux show both through `osascript`, `zenity` or `kdialog`, and without any of
  them the report goes to the error output only.
- Setting: None
- Default: mandatory
- Justification: The Windows controls are not part of the game's art. Windows draws them in the
  colours, fonts and style of whichever Windows version runs the game, so they show the platform
  the original was built for rather than a picture its artists made, and there is no single look
  of the original to reproduce. A setting could only make the rebuild draw imitations of one
  Windows version's controls, which would be no closer to the original than the rebuild's own
  controls and would clash with the screens around them. Real native controls do not fit the
  rebuild's window either: it scales and letterboxes the drawing area in a resizable window or a
  borderless full screen, with no menu bar above it (DEV-GFX-001), where a native menu or dialog
  would open at the desktop's scale outside the picture. The choices and their results are the
  original's, so a player loses nothing.
- Tests: tests/Rechaos.Tests/OriginalNewGameExperimentTests.Orders.cs, tests/Rechaos.Tests/OriginalNewGameExperimentTests.Closes.cs, tests/Rechaos.Tests/LeavePromptTests.cs, tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no

Every Windows control the spec records, and what the rebuild does in its place. An entry in the
last column records how that replacement departs from the original beyond its look; where it
says None, no other entry records a difference there.

| Original control | Evidence | The rebuild | Entry |
|---|---|---|---|
| Menu bar with File, Edit, Options, Comm and Help, its check marks and greyed items, and Alt to open it | SCR-UI-009, FND-UI-008, FND-UI-021 | The title screen's buttons, the Escape menu, the Options screen and keys | DEV-UI-019 |
| Popup order menus 1 and 2 of a gang card and 3 and 5 of the group order strip | SCR-UI-004, FND-UI-021, FND-UI-057 | A drawn order panel listing the same orders | DEV-UI-021 |
| Common Open and Save As file dialogs | FND-PLATFORM-003, FND-SAVE-002 | The save browser's nine named slots | DEV-UI-011 |
| Message box when a saved game cannot be loaded | FND-SAVE-002 | A line in the save browser; the original's save files are not read | DEV-SAVE-001 |
| Dialog 129: save first, cancel, or go on without saving | RULE-UI-015, FND-UI-022 | A prompt in the Escape menu with the same three answers and results; an online match does not ask | DEV-NET-001 |
| Dialog 139 with edit control 1007: the player's name | SCR-SETUP-001, FND-SETUP-005, FND-UI-022 | The name is edited in place on the setup card, and OK and Cancel are Enter and Escape | None |
| Dialog 136: notice before Thousands of Colors or Full Screen is switched | RULE-UI-014, SCR-UI-009 | Nothing: full screen switches at once with F11, and Thousands of Colors does not exist | DEV-OPTIONS-003, DEV-UI-016 |
| Dialogs 132, 135 and 20007 at start | RULE-UI-013, FND-UI-022 | The rebuild's own report, in a system dialog; 20007 cannot arise | DEV-GFX-001 |
| Dialog 137: an image file failed to load and play goes on | FND-UI-022 | Nothing: an image file that is missing is left undrawn and play goes on without a notice; an asset pack without its manifest, or of another format, ends the start with the rebuild's report, after a start on macOS or Linux has offered to import a current one | None |
| Network dialogs 130, 131, 138, 140, 141, 143, 144 and 20004 to 20006, edit control 1007 of dialog 20000, list box 1003 of dialog 20002, the `DIALDIALOG` and `DIRECTDIALOG` dialogs with their combo box 1001, the Telephony dialogs of `lineTranslateDialog` and `lineConfigDialogEdit`, the serial port's `CommConfigDialogA`, the modem control panel, and the network message boxes | FND-UI-022, FND-NET-005, FND-PLATFORM-013 | The rebuild's Online screens | DEV-NET-001 |
| The Windows help program, which Help Topics never starts | RULE-HELP-001 | The rebuild's help viewer | DEV-HELP-001 |

The name editor's look is this entry's, and its editing is the original's: what the player can
type and where the caret goes follow the edit control, and the ten characters OK keeps, with
every character outside space to `Z` made a space, follow the game's copy of the text after the
dialog closes (FND-UI-022). The card editor does not yet have the control's caret and editing
keys; that gap is one of editing behaviour, which this entry does not cover.

Decided 2026-10-07 ("Draw the game's own controls in place of the Windows controls").
