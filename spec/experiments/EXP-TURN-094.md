---
id: EXP-TURN-094
title: Does a family-5 computer gang in a hostile human's sector draw only human gangs in Big Man, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-094.json
---

## Question

EXP-TURN-093 reaches the family-5 draw from human gangs only in Acceptance.
Does a Big Man match, whose objective families plan beside it, reach the same
draw, as RULE-AI-024 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Big Man (`--scenario 8`), Mentality 3 (`--mentality 3`),
thirty-seven Done presses (`--end-turns 37`) and `--seed 15`. The human gives
no orders. Before the thirtieth Done press the probe writes 5 into the
`family` of the planning records in every roster slot below 10 of the computer
players that held a gang at the time (FMT-STATE-007, `--families
30:1:0:5,30:1:1:5,30:1:2:5,30:1:3:5,30:1:4:5,30:1:5:5,30:1:6:5,30:1:7:5,30:2:0:5,30:2:1:5,30:2:2:5,30:2:3:5,30:2:4:5,30:2:5:5,30:2:6:5,30:2:7:5,30:3:0:5,30:3:1:5,30:3:2:5,30:3:3:5,30:3:4:5,30:3:5:5,30:3:6:5,30:3:7:5,30:3:8:5,30:3:9:5,30:4:0:5,30:4:1:5,30:4:2:5,30:4:3:5,30:4:4:5,30:4:5:5,30:4:6:5,30:4:7:5,30:4:8:5,30:4:9:5,30:5:0:5,30:5:1:5,30:5:2:5`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 20, with families 0, 4, 5 and 7 written into every
computer gang at turn 20 or 30, and recorded which of a list of unreached
planner branches each match took.

## Observations

The run made 9902 calls of `roll` over thirty-seven Done presses. At the end
`elapsed_turns` is 37 and no player's `controller` has changed.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. The rebuild reaches the
family-5 draw from the human players' gangs only, and a refused draw.

## Conclusion

The run agrees with RULE-AI-024 for the draw from the human players' gangs
only.
