---
id: EXP-UI-015
title: Do the title screen, the credits and the setup screen look the same in the rebuild?
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
the rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 6 --seed 3 --end-turns 0
--white-key --title-capture --credits-capture --setup-capture`. The probe holds the left
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
time, before the run writes its own settings, and then goes on with the run as
EXP-TURN-001 does. The rest of the run only gives the fixture its setup rolls.

## Observations

Both breakpoints were hit before the setup screen opened, and all three copies
were kept. The first shows the title art of FND-UI-009 and nothing over it, the
second the credits of FND-UI-007 over the whole drawing area. The space bar
closed the credits. The third shows the setup screen with Greed, its
description in five lines, the one-year length, Criminal and no planning limit
selected, and one human player in slot 0 with a red bar and a green name. The
run then made 325 calls of `roll`. Its data dump holds the six colour records
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
credits and the setup screen match everywhere. A first comparison of the setup
screen found the rebuild without the scenario's title and description, with
the background's dim bar on the card and with the name in the player's colour
nine pixels left of the original's.

## Conclusion

The run supports the drawn elements of SCR-UI-001 and SCR-UI-002: the title
art and the credits each copied opaquely at `(0,0,640,460)`, and a key closing
the credits. It supports SCR-SETUP-001 for the screen as New Game first opens
it with the options at their initialized values, FND-SETUP-019 for the Greed
description, and FND-SETUP-014 for the card of slot 0.
