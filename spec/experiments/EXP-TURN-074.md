---
id: EXP-TURN-074
title: Do family-4 computer gangs draw attacks at weight 10 and take a sector after repeated Moves, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-074.json
---

## Question

EXP-TURN-040 compares family-4 plans with no hostile human gang in sight. When
a family-4 gang stands where its weight is 10, does it draw an attack target
and keep or drop the Attack by the strength test, and after repeated Moves
does it plan Control, as RULE-AI-023 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Kill 'Em All (`--scenario 4`), Mentality 3 (`--mentality
3`), eleven Done presses (`--end-turns 11`) and `--seed 2`. The human gives no
orders. Before the seventh Done press the probe writes 4 into the `family` of
the planning records in roster slots 0 to 5 of each computer player that holds
them (FMT-STATE-007, `--families
7:1:0:4,7:1:1:4,7:1:2:4,7:1:3:4,7:1:4:4,7:1:5:4,7:2:0:4,7:2:1:4,7:2:2:4,7:2:3:4,7:2:4:4,7:2:5:4,7:3:0:4,7:3:1:4,7:3:2:4,7:3:3:4,7:3:4:4,7:3:5:4,7:4:0:4,7:4:1:4,7:4:2:4,7:4:3:4,7:4:4:4,7:4:5:4,7:5:0:4,7:5:1:4,7:5:2:4,7:5:3:4,7:5:4:4`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played Greed, Power
and Kill 'Em All at every Mentality for seeds 1 to 5 with each family written
at turn 3 or 7, and recorded which planner branches no recorded run had
reached. This run reaches the RULE-AI-023 branches named under Results.

## Observations

The run made 2907 calls of `roll` over eleven Done presses. At the end
`elapsed_turns` is 11 and every player is still active.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. The rebuild reaches the
weight-10 attack draw with an accepted and a refused target, and the Control
planned after repeated Moves.

## Conclusion

The run agrees with RULE-AI-023 for the attack draw at weight 10, both
outcomes of the strength test, and Control after repeated Moves.
