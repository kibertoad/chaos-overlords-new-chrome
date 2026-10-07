---
id: EXP-TURN-088
title: Does a family-7 computer gang that draws a target it cannot or will not attack go on with its research, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-088.json
---

## Question

No other recorded run has a family-7 attack draw end without an Attack. When
the drawn target fails the strength test, or its owner is not hostile, does
the gang go on to its Equip and research steps, as RULE-AI-026 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Siege (`--scenario 6`), Mentality 3 (`--mentality 3`),
twenty Done presses (`--end-turns 20`) and `--seed 18`. The human gives no
orders. Before the twentieth Done press the probe writes 7 into the `family`
of the planning records in every roster slot below 10 of the computer players
that held a gang at the time (FMT-STATE-007, `--families
20:1:0:7,20:1:1:7,20:1:2:7,20:1:3:7,20:1:4:7,20:1:5:7,20:1:6:7,20:1:7:7,20:1:8:7,20:1:9:7,20:2:0:7,20:2:1:7,20:2:2:7,20:2:3:7,20:2:4:7,20:2:5:7,20:2:6:7,20:2:7:7,20:2:8:7,20:2:9:7,20:3:0:7,20:3:1:7,20:3:2:7,20:3:3:7,20:3:4:7,20:3:5:7,20:3:6:7,20:3:7:7,20:3:8:7,20:3:9:7,20:4:0:7,20:4:1:7,20:4:2:7,20:4:3:7,20:4:4:7,20:4:5:7,20:4:6:7,20:4:7:7,20:4:8:7,20:4:9:7,20:5:0:7,20:5:1:7,20:5:2:7,20:5:3:7,20:5:4:7,20:5:5:7,20:5:6:7,20:5:7:7,20:5:8:7,20:5:9:7`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 20, with families 0, 4, 5 and 7 written into every
computer gang at turn 20 or 30, and recorded which of a list of unreached
planner branches each match took.

## Observations

The run made 6844 calls of `roll` over twenty Done presses. At the end
`elapsed_turns` is 20 and no player's `controller` has changed.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off and writes
the same families before the same Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state, the planning
records included. The rebuild reaches the family-7 draw that ends without an
Attack and goes on to the later steps.

## Conclusion

The run agrees with RULE-AI-026 for a family-7 draw that gives no Attack.
