---
id: EXP-TURN-081
title: Does a family-0 computer gang whose previous action was Snitch move, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-081.json
---

## Question

No other recorded run gives a family-0 computer gang a turn after a Snitch.
Does it plan a Move through the sector selector, as RULE-AI-019 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Big Man (`--scenario 8`), Mentality 3 (`--mentality 3`),
thirteen Done presses (`--end-turns 13`) and `--seed 9`. The human gives no
orders. Before the eighth Done press the probe writes 0 into the `family` of
the planning records in every roster slot of the computer players that held a
gang at the time (FMT-STATE-007, `--families
8:1:0:0,8:1:1:0,8:1:2:0,8:1:3:0,8:2:0:0,8:2:1:0,8:2:2:0,8:3:0:0,8:3:1:0,8:3:2:0,8:3:3:0,8:4:1:0,8:4:2:0,8:4:3:0,8:5:0:0,8:5:1:0`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played every scenario
at every Mentality for seeds 6 to 12 with each family written into every
computer gang at turn 3, 8 or 14, and recorded which planner branches no
recorded run had reached. This run reaches the RULE-AI-019 branches named
under Results.

## Observations

The run made 1833 calls of `roll` over thirteen Done presses. At the end
`elapsed_turns` is 13 and every player is still active.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. The rebuild reaches the
family-0 Move after a Snitch.

## Conclusion

The run agrees with RULE-AI-019 for a family-0 gang after a Snitch.
