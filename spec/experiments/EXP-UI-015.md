---
id: EXP-UI-015
title: Do the title screen and the credits look the same in the rebuild?
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
before New Game, and the credits that Help, About opens over it, draw the same
pixels in the rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 6 --seed 3 --end-turns 0
--white-key --title-capture --credits-capture`. The probe holds the left
button and waits for the breakpoint at `0x004615D0` (FND-UI-055) before it
posts New Game. When the breakpoint is hit it releases the button, lets the
program run for two seconds and copies the drawing area. It then posts About's
command `0x8003` (FND-UI-008), waits for the breakpoint at `0x00464DFB`
(FND-UI-055), lets the program run two seconds, copies the drawing area again
and posts a press and release of the space bar. It then goes on with the run
as EXP-TURN-001 does. The rest of the run only gives the fixture its setup
rolls.

## Observations

Both breakpoints were hit before the setup screen opened, and both copies were
kept. The first shows the title art of FND-UI-009 and nothing over it, the
second the credits of FND-UI-007 over the whole drawing area. The space bar
closed the credits, the run reached setup and made 325 calls of `roll`. A
first run that took only the title copy gave the same title capture.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the copies with the
rebuild's title screen and credits, which the game draws with
`--reference-frame title` and `--reference-frame credits`. Leaving out the
rebuild's buttons, line under the logo and credit line (DEV-UI-019), its Intro
button (DEV-VIDEO-003) and its version (DEV-UI-012), the title screen matches.
The credits match everywhere.

## Conclusion

The run supports the drawn elements of SCR-UI-001 and SCR-UI-002: the title
art and the credits each copied opaquely at `(0,0,640,460)`, and a key closing
the credits.
