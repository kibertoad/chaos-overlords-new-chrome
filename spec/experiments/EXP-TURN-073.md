---
id: EXP-TURN-073
title: Does a family-7 computer gang with no item left to research fall back to family 0 and move, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-073.json
---

## Question

No other recorded run gives a family-7 computer gang a turn where it finds no
item to research. Does it scan the item types in the fallback order and the
fixed miscellaneous list, then take family 0 and plan a Move through sector
selector mode 5, as RULE-AI-026 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
seven Done presses (`--end-turns 7`) and `--seed 3`. The human gives no
orders. Before the third Done press the probe writes 7 into the `family` of
the planning records in roster slots 0 and 1 of each computer player
(FMT-STATE-007, `--families
3:1:0:7,3:1:1:7,3:2:0:7,3:2:1:7,3:3:0:7,3:3:1:7,3:4:0:7,3:4:1:7,3:5:0:7,3:5:1:7`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played Greed, Power
and Kill 'Em All at every Mentality for seeds 1 to 5 with each family written
at turn 3 or 7, and recorded which planner branches no recorded run had
reached. This run reaches the RULE-AI-026 branches named under Results.

## Observations

The run made 808 calls of `roll` over seven Done presses. At the end
`elapsed_turns` is 7 and every player is still active.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. The rebuild takes the
fallback research scans, finds no item on the fixed list, sets the gang's
family to 0 and plans a Move through selector mode 5.

## Conclusion

The run agrees with RULE-AI-026 for the fallback scans and the fixed item
list, and for a family-7 gang that runs out of research and changes family.
