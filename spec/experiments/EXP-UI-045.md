---
id: EXP-UI-045
title: Does the detailed sector screen draw the site progress meter for a sector another player owns?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-045.json
---

## Question

When the active human opens the detailed sector screen of a sector that a
computer player owns, and that sector holds a site with progress above 0 and
below its Resistance and a site whose progress equals its Resistance, does the
screen draw either site's progress meter (FND-UI-018, FND-UI-070)?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-UI-006 (`--scenario 9 --mentality 1 --seed 5490 --end-turns 28`,
`--white-key`), with these steps after the dump instead of that run's:
`--order-steps exit,exit,exit,open:16,shot:SCR-UI-004,back,open:61,shot:SCR-UI-004`.
The three Exit steps close the panels the planning entry opened; the others
double-click sector 16, capture, press the back control, double-click sector
61 and capture, each shot taken as in EXP-UI-006.

The human is player 0. At the dump, read from the fixture's `owner`,
`sites[k].definition` and `sites[k].progress` (FMT-STATE-002) and the
definitions' `resistance` (FMT-DATA-001):

- sector 16 is owned by player 5; site slot 0 has progress above 0 and below
  its Resistance, slot 1 progress equal to its Resistance, slot 2 none;
- sector 61 is owned by player 3; slot 0 has progress equal to its Resistance,
  slot 1 progress above 0 and below it, slot 2 none.

The human has no gang in either sector, and no gang there of any player is
visible to it.

## Observations

The run made the same 14892 calls of `roll` with the same bounds and results
as EXP-TURN-026. Both shots were kept:

| Step | Screens | Selected sector | Marker frame | Pump counter | Frame counter |
|---|---|---|---|---|---|
| 4 | SCR-UI-004 | 16 | 8 | 6 | 6 |
| 7 | SCR-UI-004 | 61 | 7 | 0 | 0 |

In both captures the 100-by-3 area at offset `(10,59)` of each of the three
site portraits, `(96, 287 + 66k)`, holds the red track of the site frame over
its whole width, rows `(255,148,148)`, `(247,0,0)` and `(148,0,0)`, with no
green pixel: all six areas have the same digest. No progress meter is drawn,
neither for the sites in progress nor for the complete ones.

After each `open` step the viewed player `0x00487B8C` read -1, and the
Overlord bar shows no marker beside any portrait, as FND-UI-017 reads the
bar's redraw for a sector where no seat has a gang the active player sees.

## Results

A test of the rebuild compares both captures, with the
three site progress meter areas as elements of their own. Leaving out the
cash row (DEV-UI-006) and the Tolerance value (DEV-UI-007), every element
matches, the meters included.

## Conclusion

The run supports FND-UI-070: the meter is not drawn for an active player who
does not own the sector, whatever the site's progress. With EXP-UI-050, which
shows the meter drawn for the owner, it settles the meter row of SCR-UI-004.
It also supports FND-UI-017's reading that the Overlord bar draws no marker
when the active player sees no gang in the sector.
