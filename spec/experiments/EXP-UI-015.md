---
id: EXP-UI-015
title: Do the title screen, the credits and the setup screen, before and after presses on it, look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-015.json
---

## Question

Do the title screen, as the program shows it after the intro movies and
before New Game, the credits that Help, About opens over it, and the setup
screen New Game first opens when no options are stored, draw the same pixels in
the rebuild as in the original? Does the setup screen still match after presses
on its cards, buttons and lights?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 6 --mentality 1 --time-limit 0
--seed 3 --end-turns 0 --white-key --title-capture --credits-capture
--setup-capture --setup-steps <steps>`, with the steps below. The probe holds the left
button and waits for the breakpoint at `0x004615D0` (FND-UI-055) before it
posts New Game. When the breakpoint is hit it releases the button, lets the
program run for two seconds and copies the drawing area. It then posts About's
command `0x8003` (FND-UI-008), waits for the breakpoint at `0x00464DFB`
(FND-UI-055), lets the program run two seconds, copies the drawing area again
and posts a press and release of the space bar. It then writes 0 to the
objective `0x00487858`, 1 to the Mentality `0x00487850` and 0 to the planning
limit `0x00487854`, their initialized values (FND-OPTIONS-001), because the
registry key of this installation holds other values, and posts New Game.
Two seconds after the setup screen opens it copies the drawing area a third
time. It then posts the setup steps, before the run writes its own settings,
and goes on with the run as EXP-TURN-001 does. The rest of the run only gives
the fixture its setup rolls.

A `strip:x:y` step posts a press and release at a window point (FND-SETUP-005
and FND-SETUP-013 give the rectangles), `drag:x:y:x2:y2` a press, eight moves
with the button down and the release at the second point, and `shot` a copy of
the drawing area:

| Steps | Presses |
|---|---|
| 0 to 1 | The up band of card 0, shot |
| 2 to 3 | Add, shot |
| 4 to 5 | Card 1, shot |
| 6 to 9 | Kill 'Em All, turn time 2 minutes, Homicidal Maniac, shot |
| 10 to 12 | Greed, 2 years, shot |
| 13 to 14 | Remove, shot |
| 15 to 17 | The down band of card 0, a drag from Add to `(300, 200)`, shot |
| 18 to 23 | Add five times, shot |
| 24 to 25 | Add, shot |
| 26 to 31 | Remove five times, shot |
| 32 to 33 | Remove, shot |

## Observations

Both breakpoints were hit before the setup screen opened, and all three copies
were kept. The first shows the title art of FND-UI-009 and nothing over it, the
second the credits of FND-UI-007 over the whole drawing area. The space bar
closed the credits. The third shows the setup screen with Greed, its
description in five lines, the one-year length, Criminal and no planning limit
selected, and one human player in slot 0 with a red bar and a green name.

All eleven setup copies were kept. The up band gave card 0 the next portrait
and the down band gave it back. Add put a second human in slot 1 and selected
it, the press on card 1 then selected card 1, which already was, and the
presses on the left panel lit Kill 'Em All, with no length lit, 2 minutes and
Homicidal Maniac, then Greed and 2 years. Remove took out the selected human
of slot 1 and selected card 0. The drag from Add added no one. Five presses of
Add filled the six slots; the sixth press changed nothing on the screen. Five
presses of Remove left one human in slot 0, and the sixth changed nothing. The
run then made 325 calls of `roll`.

An earlier run also dragged card faces from card 0 to cards 1 and 4. One such
drag, in another run with fewer steps, swapped the two cards as FND-SETUP-005
reads, but in two longer runs drags from card 0 only selected it or changed
nothing. A drag posted this way is not reliable, so the run leaves them out. The probe
posted the moves as `WM_MOUSEMOVE` and did not write the two pointer points of
FND-UI-020, one of which the window procedure takes from the desktop cursor, so
the unreliable drags may come from the probe rather than the game, and this run
does not settle card-face drags. EXP-UI-030 repeats them with a probe that
writes both points, and they act as FND-SETUP-005 reads in every run. Its data dump holds the six colour records
at `0x004ABC18` as (255,0,0), (0,255,0), (0,0,255), (255,255,0), (255,0,255)
and (0,255,255). Earlier runs that took only the title copy, and the title and
the credits, gave the same captures of them. One that did not write the options
showed the objective the registry held, Kill 'Em All.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the copies with the
rebuild's title screen, credits and setup screen, which the game draws with
`--reference-frame title`, `credits` and `setup`. Leaving out the rebuild's
buttons, line under the logo and credit line (DEV-UI-019), its Intro button
(DEV-VIDEO-003) and its version (DEV-UI-012), the title screen matches. The
credits and the setup screen match everywhere, the setup screen also after
each of the eleven setup copies, with the presses before it made in the
rebuild as `--reference-clicks` and the drag as a press, a move and a release.
A first comparison of the setup
screen found the rebuild without the scenario's title and description, with
the background's dim bar on the card and with the name in the player's colour
nine pixels left of the original's.

## Conclusion

The run supports the drawn elements of SCR-UI-001 and SCR-UI-002: the title
art and the credits each copied opaquely at `(0,0,640,460)`, and a key closing
the credits. It supports SCR-SETUP-001 for the screen as New Game first opens
it with the options at their initialized values and after the presses,
FND-SETUP-019 for the Greed and Kill 'Em All descriptions, and FND-SETUP-014
for the cards. It supports RULE-SETUP-002 for the case where no scenario is
stored, RULE-SETUP-009 for selection and the portrait bands, RULE-SETUP-010 for
the first setup of a session and for Add and Remove up to their limits, and
RULE-UI-001 for a push button released inside and outside.
