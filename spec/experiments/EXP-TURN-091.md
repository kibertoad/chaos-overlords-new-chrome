---
id: EXP-TURN-091
title: Does a family-0 computer gang whose weight-10 attack draw fails its strength test give up the Attack, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-091.json
---

## Question

No other recorded run has a family-0 gang, in the branch whose strength test
reads the record at the slot numbered by its sector (BUG-AI-007), draw a
target at weight 10 that fails the test. When it fails, does the gang plan no
action and clear its focus and coverage sector, as RULE-AI-019 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Armageddon (`--scenario 9`), Mentality 1 (`--mentality
1`), thirty-one Done presses (`--end-turns 31`) and `--seed 12`. The human
gives no orders. Before the thirtieth Done press the probe writes 0 into the
`family` of the planning records in every roster slot below 10 of the computer
players that held a gang at the time (FMT-STATE-007, `--families
30:1:0:0,30:1:1:0,30:1:2:0,30:1:3:0,30:1:4:0,30:1:5:0,30:1:6:0,30:1:7:0,30:1:8:0,30:1:9:0,30:2:0:0,30:2:1:0,30:2:2:0,30:2:3:0,30:2:4:0,30:2:5:0,30:2:6:0,30:2:7:0,30:2:8:0,30:2:9:0,30:3:0:0,30:3:1:0,30:3:2:0,30:3:3:0,30:3:4:0,30:3:5:0,30:3:6:0,30:3:7:0,30:3:8:0,30:3:9:0,30:4:0:0,30:4:1:0,30:4:2:0,30:4:3:0,30:4:4:0,30:4:5:0,30:4:6:0,30:4:7:0,30:4:8:0,30:4:9:0,30:5:0:0,30:5:1:0,30:5:2:0,30:5:3:0,30:5:4:0,30:5:5:0,30:5:6:0,30:5:7:0,30:5:8:0,30:5:9:0`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 20, with families 0, 4, 5 and 7 written into every
computer gang at turn 20 or 30, and recorded which of a list of unreached
planner branches each match took.

## Observations

The run made 18267 calls of `roll` over thirty-one Done presses. At the end
`elapsed_turns` is 31 and no player's `controller` has changed.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off and writes
the same families before the same Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state, the planning
records included. The rebuild reaches the refused family-0 draw at weight 10 and
plans no action for that gang.

## Conclusion

The run agrees with RULE-AI-019 and BUG-AI-007 for a refused family-0 draw at
weight 10.
