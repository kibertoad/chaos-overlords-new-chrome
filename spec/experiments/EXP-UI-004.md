---
id: EXP-UI-004
title: Which gang-status markers does the map show while the Hire dock changes at the first planning entry?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-004.json
---

## Question

Which gang-status markers does the city map show at the first planning entry,
and how does each change of a hire order on the Hire dock redraw them?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-HIRE-001, with `--gang-markers` added. With `--gang-markers` the probe logs each drawing of the gang-status marker
function `fn_00412BF7(player, sector)` (FND-UI-024) at its three copies: at
`0x00412DFB` the frame of a sector holding the player's gang, read from
`[ebp-4]`; at `0x00412EB5` the copy of the saved cell back over the sector in
`0x004906A4`; and at `0x00412FF8` frame 8 drawn at the sector. It also logs
each start of the full city redraw `fn_004123CC`, and keeps the log from the
last full redraw before the dump on, each drawing tagged with the post-dump
step it came in.

## Observations

The run made the same 310 calls of `roll` with the same bounds and results as
EXP-HIRE-001, and kept the same `hire_orders` after each step. The human's
gangs stand in its Headquarters sector 12, with no orders.

- The full redraw at the dump drew frame 2 at sector 12; after it, before the
  dump, frame 8 was drawn twice at sector -1.
- Each accepted drop or Reject press drew the marker of the sector the dock
  kept before it, -1 when no offer was ordered into a sector, then of the
  sector an offer is now ordered into: frame 6 at sector 12 while a hire is
  ordered there, frame 2 once it is not. The drop on sector 0, which the dock
  refuses, drew nothing.
- No drawing reached the copy back.

## Results

`TheCityKeepsTheOriginalsGangMarkers` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Markers.cs` replays the
run, takes each hire step as `TheHireDockSetsTheOriginalsOrders` does, and
compares the map the log leaves after each step with the rebuild's marker map.
They are the same after every step.

## Conclusion

The run agrees with RULE-UI-006 for frames 2 and 6, and with the dock's
redraws it describes.
