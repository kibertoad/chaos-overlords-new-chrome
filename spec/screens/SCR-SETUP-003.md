---
id: SCR-SETUP-003
title: Player name dialog with one edit control, OK and Cancel, opened from a setup player card
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-068, FND-UI-022, SRC-WIN32-EDIT, SRC-WIN32-EDIT-TEXT, SRC-WIN32-EM-LIMITTEXT, SRC-WIN32-CARETS, SRC-WIN32-DIALOG]
conflicting: []
split_with: []
related: [SCR-SETUP-001, RULE-SETUP-009]
---

## Drawn elements

The dialog is a Windows dialog box from the template
`Chaos Overlords.exe#DIALOG/139`, drawn by Windows and not by the game.
Rectangles are in dialog units, which Windows turns into pixels from the
metrics of the dialog's font, MS Sans Serif at 8 points; the dialog's own
rectangle is relative to the top-left corner of the game window's client area.

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Dialog box with a modal frame and no title bar | `Chaos Overlords.exe#DIALOG/139` | None | `(110, 100, 200, 70)` | Always | FND-UI-068 |
| Prompt | The template's static text, 32 characters | None | `(8, 8, 179, 11)` | Always | FND-UI-068 |
| Edit box with a border | None | The text typed, scrolled sideways to keep the caret in view | `(8, 20, 179, 13)` | Always | FND-UI-068, SRC-WIN32-EDIT |
| Selection | None | The selected characters, highlighted | Over the selected characters | While text is selected; `ES_NOHIDESEL` keeps it while the focus is on a button | SRC-WIN32-EDIT |
| Caret | None | The insertion point, as a blinking inverted rectangle | At the insertion point | While the edit box has the focus | SRC-WIN32-EDIT, SRC-WIN32-CARETS |
| Cancel button | The template's 6-character label | None | `(84, 47, 50, 14)` | Always | FND-UI-068 |
| OK button, the default push button | The template's 2-character label | None | `(142, 47, 50, 14)` | Always | FND-UI-068 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Edit box | `(8, 20, 179, 13)` | Always | A press moves the insertion point to the press, or with Shift extends the selection to it; moving with the button held changes the selection; a double click selects the word under the pointer | SRC-WIN32-EDIT |
| OK | `(142, 47, 50, 14)` | Always | Closes the dialog with result 1: a text that is not empty becomes the name (FND-UI-068) | FND-UI-022, FND-UI-068 |
| Cancel | `(84, 47, 50, 14)` | Always | Closes the dialog with result 2; the name is unchanged | FND-UI-022, FND-UI-068 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| A key that gives a character | The edit box has the focus | Replaces the selection with the character, or inserts it at the insertion point; a lower-case letter goes in as a capital; nothing goes in once the text holds 32,767 characters | SRC-WIN32-EDIT, SRC-WIN32-EM-LIMITTEXT, FND-UI-068 |
| Left, Right, Home, End, with or without Shift | The edit box has the focus | Moves the insertion point, or with Shift extends the selection | SRC-WIN32-EDIT, SRC-WIN32-DIALOG |
| Backspace, Delete | The edit box has the focus | Deletes the selection, or the character before or after the insertion point | SRC-WIN32-EDIT |
| Ctrl+C, Ctrl+X, Ctrl+V, Alt+Backspace | The edit box has the focus | Copies, cuts, pastes and undoes | SRC-WIN32-EDIT |
| Enter | Always | As OK | SRC-WIN32-DIALOG, FND-UI-068 |
| Escape | Always | As Cancel | SRC-WIN32-DIALOG, FND-UI-022 |
| Tab, Shift+Tab | Always | Moves the focus between the edit box, OK and Cancel | SRC-WIN32-DIALOG, FND-UI-068 |

## Other input

None.

## Sounds

None known.

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open, the edit box empty and focused | RULE-SETUP-009 opens the dialog: the procedure focuses the dialog, which passes the focus to the edit box, the first control with `WS_TABSTOP` | OK, Cancel, Enter or Escape | FND-UI-068, SRC-WIN32-DIALOG |
| Closed | The dialog ends; the game copies the first ten characters of the text into the name, each character outside space to `Z` as a space, unless the result is not 1 or the text is empty | None | FND-UI-022, FND-UI-068 |

## Timing

The caret blinks at the blink time set in the Windows Control Panel
(SRC-WIN32-CARETS).

## Differences between builds

None known.

## Open questions

- No run of the original has typed into the dialog. The keys come from the
  documentation of the Windows edit control and dialog manager, not from a
  capture of the game; the probe's `name:` setup step can make that run.
- What each navigation key does in detail is not recorded: the sources say only
  that the keyboard moves the insertion point and selects text. In particular,
  whether Left and Right with a selection and without Shift move from the
  insertion point or collapse the selection to one of its ends is not known.
- Where the dialog lands on the screen in pixels, the pixel size of the caret
  and the colours of the frame, box, selection and buttons depend on the
  Windows version and its settings, and no capture records them.
- Whether Ctrl+Left and Ctrl+Right move by words, and what the right button's
  context menu offers, depends on the Windows version.
