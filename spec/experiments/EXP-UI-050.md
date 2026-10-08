---
id: EXP-UI-050
title: Does the detailed sector screen draw the site progress meter for the active player's own sector?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-050.json
---

## Question

When the active human opens the detailed sector screen of a sector it owns,
which holds a site with progress above 0 and below its Resistance, does the
screen draw that site's progress meter, at the length RULE-UI-005 gives? This
is the contrast to EXP-UI-045.

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-065 (`--scenario 0 --mentality 0 --seed 4`, with its orders and
hires), with four Done presses (`--end-turns 4`) in place of twelve,
`--white-key`, and after the dump `--order-steps
exit,exit,exit,open:33,shot:SCR-UI-004`. The three Exit steps close any panel
the planning entry opened; the others double-click sector 33, the human's
headquarters, and capture as in EXP-UI-006.

The human is player 0. At the dump, read from the fixture's `owner`,
`sites[k].definition` and `sites[k].progress` (FMT-STATE-002) and the
definitions' `resistance` (FMT-DATA-001), sector 33 is owned by player 0, and:

- site slot 0 has progress 0 and a Resistance of 0;
- slot 1 has progress above 0 and below its Resistance, from the recurring
  Influence EXP-TURN-065 orders from turn 2;
- slot 2 has progress 0 and a Resistance above 0.

## Observations

The run made 693 calls of `roll`, the same, with the same bounds and results,
as the first 693 of EXP-TURN-065. The open step showed two of the human's
gangs as cards, and the shot was kept with the selected sector 33, marker frame
7, pump counter 1 and frame counter 1.

The capture shows the meters at offset `(10,59)` of the portraits:

- slot 0 green over the whole 100 pixels, as `site_meter_length` gives for a
  Resistance of 0;
- slot 1 green over the first 75 pixels and the red track over the other 25,
  the truncated percentage of its progress;
- slot 2 the red track alone, since a length of 0 copies nothing.

The green rows are `(148,255,148)`, `(0,247,0)` and `(0,140,0)` and the red
ones `(255,148,148)`, `(247,0,0)` and `(148,0,0)`, as FND-UI-036 reads them.

## Results

A test of the rebuild compares the capture, with the
three site progress meter areas as elements of their own. Leaving out the cash
row (DEV-UI-006) and the Tolerance value (DEV-UI-007), every element matches.

## Conclusion

The run supports FND-UI-070 for the owner and RULE-UI-005's site meter length,
including the full meter for a Resistance of 0 and no copy for a length of 0.
With EXP-UI-045 it settles the meter row of SCR-UI-004.
