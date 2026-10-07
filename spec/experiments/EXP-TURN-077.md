---
id: EXP-TURN-077
title: Does the family-6 guard target skip weight-10 sectors another family-6 gang already covers, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-077.json
---

## Question

No other recorded run has several family-6 gangs of one player choosing guard
targets. When a weight-10 sector is already covered by another family-6 gang,
does the selector skip it, as RULE-AI-025 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Kill 'Em All (`--scenario 4`), Mentality 3 (`--mentality
3`), eleven Done presses (`--end-turns 11`) and `--seed 2`. The human gives no
orders. Before the seventh Done press the probe writes 6 into the `family` of
the planning records in roster slots 0 to 5 of each computer player that holds
them (FMT-STATE-007, `--families
7:1:0:6,7:1:1:6,7:1:2:6,7:1:3:6,7:1:4:6,7:1:5:6,7:2:0:6,7:2:1:6,7:2:2:6,7:2:3:6,7:2:4:6,7:2:5:6,7:3:0:6,7:3:1:6,7:3:2:6,7:3:3:6,7:3:4:6,7:3:5:6,7:4:0:6,7:4:1:6,7:4:2:6,7:4:3:6,7:4:4:6,7:4:5:6,7:5:0:6,7:5:1:6,7:5:2:6,7:5:3:6,7:5:4:6`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played Greed, Power
and Kill 'Em All at every Mentality for seeds 1 to 5 with each family written
at turn 3 or 7, and recorded which planner branches no recorded run had
reached. This run reaches the RULE-AI-025 branches named under Results.

## Observations

The run made 1777 calls of `roll` over eleven Done presses. At the end
`elapsed_turns` is 11 and every player is still active.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off and writes
the same families before the same Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state, the planning
records included. The rebuild reaches a weight-10 sector that a family-6 gang
already covers, which the guard target selection skips.

## Conclusion

The run agrees with RULE-AI-025 for a weight-10 sector already covered by
another family-6 gang.
