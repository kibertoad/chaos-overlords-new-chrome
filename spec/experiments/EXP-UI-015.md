---
id: EXP-UI-015
title: Does the title screen look the same in the rebuild?
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

Does the title screen, as the program shows it after the intro movies and
before New Game, draw the same pixels in the rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 6 --seed 3 --end-turns 0
--white-key --title-capture`. The probe holds the left button and waits for
the breakpoint at `0x004615D0` (FND-UI-055) before it posts New Game. When the
breakpoint is hit it releases the button, lets the program run for two
seconds, copies the drawing area and then goes on with the run as EXP-TURN-001
does. The rest of the run only gives the fixture its setup rolls.

## Observations

The breakpoint was hit before the setup screen opened, and the copy was kept.
It shows the title art of FND-UI-009 and nothing over it. The run then reached
setup and made 325 calls of `roll`.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the copy with the
rebuild's title screen, which the game draws with `--reference-frame title`.
Leaving out the rebuild's buttons, line under the logo and credit line
(DEV-UI-019), its Intro button (DEV-VIDEO-003) and its version (DEV-UI-012),
the screen matches.

## Conclusion

The run supports SCR-UI-001's drawn elements: the title art copied opaquely at
`(0,0,640,460)`.
