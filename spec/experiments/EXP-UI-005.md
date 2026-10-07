---
id: EXP-UI-005
title: Does the incoming-only mark stay on the map until a redraw removes it?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-005.json
---

## Question

When the Hire dock orders a hire into a sector the player owns without a gang
in it, does the map show frame 8 there, and which redraws remove it?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071, then after the dump `--hire-steps
exit,exit,drag:0:19,drag:0:12,drag:1:20,drag:2:19,reject:1,drag:0:12`, taken as
in EXP-HIRE-001, and `--order-steps strip:576:266:0,strip:161:304:0`, which
presses the console's Search control and then the Search panel's Done
(SCR-SEARCH-001), with `--gang-markers`. With `--gang-markers` the probe logs each drawing of the gang-status marker
function `fn_00412BF7(player, sector)` (FND-UI-024) at its three copies: at
`0x00412DFB` the frame of a sector holding the player's gang, read from
`[ebp-4]`; at `0x00412EB5` the copy of the saved cell back over the sector in
`0x004906A4`; and at `0x00412FF8` frame 8 drawn at the sector. It also logs
each start of the full city redraw `fn_004123CC`, and keeps the log from the
last full redraw before the dump on, each drawing tagged with the post-dump
step it came in.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results as
EXP-TURN-071. The human owns sector 12, where it has no gang, and its three
gangs stand in sector 19, roster slot 1 with no order. The steps gave
`hire_orders` 19, then 12, then 12 again after the refused drop on sector 20,
then offer 2 into 19, then the snub of offer 1, then offer 0 into 12. The
log, after the dump's redraw drew frame 2 at sector 19 and frame 8 was drawn
twice at sector -1:

| Step | Drawings |
|---|---|
| drag 0 to 19 | frame 8 at -1; frame 6 at 19 |
| drag 0 to 12 | frame 2 at 19; frame 8 at 12 |
| drag 1 to 20 | none |
| drag 2 to 19 | the copy back over 12; frame 6 at 19 |
| reject 1 | frame 2 at 19; the copy back over 12; frame 8 at -1 |
| drag 0 to 12 | frame 8 at -1; frame 8 at 12 |
| Search | none |
| Done | a full redraw: the copy back over 12 for sectors 0 to 11, frame 8 at 12, then the copy back over 12 for every later sector without the player's gangs, and frame 2 at 19 |

So frame 8 showed at sector 12 after each drop there, and the full redraw
that closing the Search panel makes left it off the map.

## Results

A test of the rebuild compares the run as in EXP-UI-004, with the Search panel's
Done drawing the whole map again in the rebuild. The maps are the same after
every step.

## Conclusion

The run agrees with RULE-UI-006: frame 8 shows on a sector the dock orders a
hire into until a drawing of a sector without the player's gangs copies the
cell back, and a full redraw removes it when a later sector lacks the player's
gangs.
