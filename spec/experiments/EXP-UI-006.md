---
id: EXP-UI-006
title: Do the city, its console panels and the detailed sector screen look the same in the rebuild late in a match?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-006.json
---

## Question

At the planning entry of turn 29 of an idle Armageddon match, do the city
screen, the Game Information panel, the City Financial panel, the detailed
sector screen and the Gangs in Sector panel draw the same pixels in the
rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-026 (`--scenario 9 --mentality 1 --seed 5490 --end-turns 28`),
with `--white-key`, and after the dump `--order-steps
exit,exit,exit,shot:SCR-UI-003+SCR-HIRE-002,strip:600:58:0,shot:SCR-UI-008,strip:185:304:0,strip:576:194:0,shot:SCR-FINANCE-001,strip:185:304:0,open:33,shot:SCR-UI-004,back,strip:524:246:0,shot:SCR-UI-005,strip:161:304:0,shot:SCR-UI-003`.
The three Exit steps close the panels the planning entry opened. Each `shot`
step copies the drawing area twice, as the endpoint capture does, and keeps
the copy when both agree and neither the Overlord bar's marker counter
`0x00487B90` (FND-UI-038) nor the pump's counter `0x00487804` (FND-UI-017)
moved; it records both counters. It also records the selected sector
`0x004ABC80` and, as in EXP-UI-008, the frame counter: the pump's counter when
the open panel finished sliding in (FND-UI-051), or the pump counter when no
panel slid in. The other steps press the console's Game
Information control and its OK, the Financial control and the panel's Exit,
double-click sector 33, press the back control, press Gangs in Sector and its
Exit.

## Observations

The run made the same 14892 calls of `roll` with the same bounds and results
as EXP-TURN-026. The first Exit step closed a panel and the other two found
none open. All six shots were kept:

| Step | Screens | Marker frame | Pump counter | Frame counter |
|---|---|---|---|---|
| 3 | SCR-UI-003, SCR-HIRE-002 | 1 | 0 | 0 |
| 5 | SCR-UI-008 | 9 | 5 | 0 |
| 8 | SCR-FINANCE-001 | 1 | 7 | 2 |
| 11 | SCR-UI-004 | 7 | 0 | 0 |
| 14 | SCR-UI-005 | 11 | 3 | 6 |
| 16 | SCR-UI-003 | 8 | 7 | 7 |

The fixture keeps the xxh3 of each bitmap and a digest of each element of
the screens named; the bitmaps are kept under `GAME_DIR/captures/`.

The captures show:

- default player names written with a space before the number (FND-SETUP-017);
- the dim zero cells of two-cell numbers as a separate cell of the font
  strip, not a tint of the lit one;
- the Gangs in Sector panel's sector cell taken from the unmarked city map,
  and its portraits at half size from the odd rows and columns of the 64-by-64
  cell;
- the selection frame of the counter less one (FND-UI-048), on the city and
  on the detailed sector screen, where it is drawn over the display's centre
  cell; behind the Financial and Gangs in Sector panels it is the frame of the
  frame counter, not of the pump counter at the capture (FND-UI-051).

## Results

A test of the rebuild replays the run, saves the endpoint, and has the rebuild's
reference frame repeat the presses from the endpoint before it draws, with the
recorded marker frame, frame counter and selected sector. It compares every
element of every screen with the original, leaving out the cash row (DEV-UI-006)
and the city's key line (DEV-UI-023). Every element of the six captures matches.

## Conclusion

The run supports SCR-UI-003, SCR-UI-004, SCR-UI-005, SCR-UI-008,
SCR-FINANCE-001 (City variant) and SCR-HIRE-002 for one late state, and
FND-UI-048 and FND-UI-051 for the selection frame's phase.
