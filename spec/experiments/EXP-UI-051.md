---
id: EXP-UI-051
title: What do the keys, presses, OK and Cancel of the player name dialog do to the edit control's text, selection and the name kept, and what does the game draw under the dialog?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200 at a 96 DPI 32-bit desktop with the caret blink time set to 530 ms, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, the setup choices the run does not set as the copy's preferences held them, and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-051.json
---

## Question

The name dialog, `Chaos Overlords.exe#DIALOG/139`, holds one edit control
(FND-UI-068). What text and selection does the control hold after each of a
series of typed characters, Left, Right, Home, End, Delete and Backspace with
and without Shift, and presses on the box? What does OK keep of a text longer
than ten characters or of characters outside space to `Z`, and what do Enter
and Escape do? How big is the caret, and how fast does it blink? Does the
game draw anything on the setup screen while the dialog is open?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 0 --seed 3 --end-turns 0
--setup-steps <steps>` with nine `name:` steps. Each presses the name band of
card 0 at `(429, 156)`, which opens the dialog (FND-SETUP-005, FND-UI-022),
posts the step's tokens to the edit control and posts OK, then reads slot 0's
12-byte name record. `{CHARhh}` posts character `hh` as `WM_CHAR`, `{VKhh}` a
press and release of virtual key `hh`, `{SHIFT}` and `{PLAIN}` set and clear
Shift in the keyboard state the probe shares with the game's thread, and
`{PRESSxx}` posts a left press and release at client point `xx` of the edit
control, on its middle row. At each `{SHOT}` the probe waits 0.3 seconds,
reads the control's text (`WM_GETTEXT`) and selection (`EM_GETSEL`), and for
1.2 seconds copies the game window and the dialog window from their own
device contexts about every 70 ms. At the first `{SHOT}` of step 0, before any
key, it also copies the game window alone twice, as a setup step's copy.

| Step | Tokens, in order |
|---|---|
| 0 | Shot |
| 1 | `abcDEFGHIJ`, shot, Home, shot, Right three times, shot, `X`, shot, Shift+Right twice, shot, Left, shot, Shift+Right twice and Right, shot, Shift+Left three times, shot, Delete, shot, Backspace, shot, Shift+Home, shot, `q`, shot |
| 2 | `ABCDEF`, Shift+Left twice, shot, Home, shot, Shift+Right twice, shot, End, shot, Shift+Left twice, shot, Right, shot, Shift+Home, shot, Left, shot, Backspace, shot, End and Delete, shot, Shift+Right, shot, Home and Shift+Left, shot |
| 3 | 50 characters, the letters, the digits and the letters `A` to `N`, shot, Home, shot, End, shot, Left five times, shot, Shift+Home, shot |
| 4 | `ABCDEFGHIJ`, presses at x 20, 60, 2 and 240, a shot after each |
| 5 | `ZZZ`, shot, Escape |
| 6 | `ENTERED`, shot, Enter |
| 7 | `A` and the ten characters `[`, `\`, `]`, `^`, `_`, `` ` ``, `{`, `|`, `}` and `~`, shot |
| 8 | Windows-1252 `0xE9` and `0xF1`, then `B`, shot |

The tokens appear in the fixture's inputs; `name_shots` holds each shot's text
and selection, and `name_entries` each step's name record.

The probe wrote none of the objective, Mentality and planning limit options.
The setup screen took the values this machine's registry gave an unelevated
process, 0, 1 and 0, which the fixture lists as the setup input
`preferences 0:1:0`; the run's copies of the setup screen show them. A run
with `--setup-steps` now writes these options, 4, 0 and 0 unless told
otherwise, so a repeat of this run adds `--setup-preferences 0:1:0`.

## Observations

The dialog opened at every press, and every shot was read. The selection is
given as `start` to `end`; the probe cannot tell which end holds the caret.

In step 1 the text after the typing was `ABCDEFGHIJ` with the selection at 10
to 10, and Home moved it to 0. Three presses of Right gave 3, `X` went in there
and gave 4, and two of Shift+Right selected 4 to 6. Left then gave 5 to 5. Two
more of Shift+Right and a Right gave 8 to 8, three of Shift+Left 5 to 8, and
Delete removed the three selected characters, leaving `ABCXDHIJ` at 5. Backspace
removed the `D` and gave 4, Shift+Home selected 0 to 4, and `q` replaced the
selection with `Q`, leaving `QHIJ` at 1.

In step 2 Shift+Left twice selected 4 to 6, Home gave 0, Shift+Right twice 0
to 2, End 6, Shift+Left twice 4 to 6, and Right then gave 5. Shift+Home
selected 0 to 5, and Left gave 0. Backspace at 0, Delete at 6 and Shift+Right
at 6 changed nothing, and Shift+Left at 0 selected nothing.

In step 3 the control held all 50 characters, with the selection at 50, then 0
after Home, 50 after End, 45 after five of Left and 0 to 45 after Shift+Home.
In step 4 the presses at x 20, 60, 2 and 240 put the selection at 3, 8, 0 and
10.

Step 5's Escape closed the dialog, step 6's Enter closed it as well, and every
other step closed at OK. The name records after the steps held these names:

| Step | Name kept |
|---|---|
| 0 | The name before the step, 9 characters, unchanged |
| 1 | `QHIJ` |
| 2 | `ABCDEF` |
| 3 | The first ten of the 50 characters |
| 4 | `ABCDEFGHIJ` |
| 5 | `ABCDEFGHIJ`, unchanged |
| 6 | `ENTERED` |
| 7 | `A` and nine spaces |
| 8 | Two spaces and `B` |

Step 7's control held the eleven characters as typed, and step 8's `0xC9`,
`0xD1` and `B`: the two lower-case accented letters went in as their capitals.

At every shot the dialog's window rectangle was `(165, 163, 306, 120)` and the
edit control's client rectangle `(182, 201, 265, 17)` in drawing-area pixels.
Between the copies of one shot, the only change apart from a passing redraw of
the OK button was one column of 13 pixels at the insertion point, black on the
box's white, from row 202 to row 214, turning on and off. The copies came
between 60 and 155 ms apart, and every time the caret stayed in one phase it
did so for between 517 and 549 ms.

The two copies of the game window at step 0 agree. They show the setup screen
the game drew before the press, with card 0 still showing its name, and
nothing where the dialog lies.

## Results

The control holds the text, the selection and the scroll of a standard edit
control with `ES_UPPERCASE`. Left and Right without Shift move the insertion
point one character from where it stands and clear the selection: with 4 to 6
selected and the caret at 6, Left gives 5; with 5 to 7 selected and the caret
at 7, Right gives 8; with 4 to 6 selected and the caret at 4, Right gives 5.
Home and End move to the ends and clear the selection, and Shift extends it
from the end where it started. Backspace and Delete remove the selection, or
the character before or after the insertion point, and do nothing at the start
or end. A typed character replaces the selection. The text runs past ten
characters, OK and Enter keep its first ten with each character outside space
to `Z` as a space, as FND-UI-068 reads, and Escape keeps the name.
`ES_UPPERCASE` turns accented Latin-1 letters into their capitals as well.

The caret is a one-pixel line as tall as the font's 13-pixel cell, inverting
what lies under it, and it blinks at the 530 ms set in Windows. The game draws
nothing while the dialog is open: the setup screen under it is the one the
press left.

## Conclusion

The dialog's keys, OK, Enter and Escape act as SCR-SETUP-003 says, and the
open question of Left and Right over a selection is settled: they move from the
insertion point. The text limit of 32,767 characters, Tab, the clipboard keys,
undo, double-click selection and the context menu were not tried.
