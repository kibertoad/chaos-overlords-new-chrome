---
id: EXP-SETUP-005
title: Do two players named with the island modifier give the same new game as one?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-SETUP-005.json
---
## Question

RULE-SETUP-005 sets every unowned sector's Crackdown to 100 once for each
player whose `modifier_islands` is set, so two such players repeat the same
writes with no further effect. EXP-SETUP-004 names one player with the
modifier. Does a second one change anything?

## Setup

As EXP-SETUP-001, with scenario 4, Mentality 1 and seed 5, and humans in slots
0 and 1, both named with the island modifier string (FND-SETUP-015). The
other four slots become computer players at Begin (RULE-SETUP-003).

## Procedure

Run the probe with `--scenario 4 --mentality 1 --seed 5 --humans
0:islands,1:islands`, then `extract`. The probe presses Begin and copies the
state once no roll has been made for eight seconds, at the first planning
entry.

## Observations

The run made 311 calls of `roll`. Both humans' `modifier_islands` flags are
set, and every unowned sector holds a `crackdown_turns` of 100.

## Results

A test of the rebuild replays the run. The
rebuild, given the same names, makes the same calls with the same bounds and
results and reaches the same state, every sector's `crackdown_turns`
included.

## Conclusion

The run agrees with RULE-SETUP-005 for two players with the island modifier:
the second changes nothing beyond what one does.
