---
id: EXP-TURN-101
title: Where does a family-13 or family-14 computer gang move in a scenario without objective sectors?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-101.json
---

## Question

RULE-AI-031 reads that families 13 and 14 call a sector selector only in Big
Man (scenario 8) and Siege (scenario 6). In any other scenario no sector is an
objective, so the gang always reaches the Move write, and the destination is
the planned target the planning record holds from the start of the pass. No
run had put a family-13 or family-14 gang in another scenario. Does the
original move such a gang to that planned target?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 1 and a two-year limit
(`turn_limit` 104).

## Procedure

As EXP-TURN-004 with `--scenario 0 --mentality 1 --turns 104 --seed 1`, six
Done presses (`--end-turns 6`) and no orders. Before the third Done press the
probe writes 13 or 14 into the `family` of the planning records of roster
slots 0 and 1 of the computer players (FMT-STATE-007, `--families
3:1:0:13,3:1:1:14,3:2:0:13,3:3:0:13,3:3:1:14,3:4:0:13,3:4:1:14,3:5:0:13,3:5:1:14`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played Greed at every Mentality for
seeds 1 to 3 with the same writes and recorded which branches of the handler
each match took.

## Observations

The run made 601 calls of `roll` over six Done presses, with Combat Results
shown at several planning entries. At the end every written gang that is
still family 13 or 14 is in sector 0 with Move planned (`ACTION_MOVE`,
FMT-STATE-001). The gang in slot 1 of player 1 holds family 0 and Chaos
planned, in sector 54, and the gang in slot 1 of player 2, which was not
written, is in sector 17.

## Results

A test of the rebuild replays the run and
writes the same families before the same Done press. The rebuild makes the same
calls with the same bounds and results and reaches the same state, the planning
records included. Each written gang reaches the handler's Move write in the
planning pass of the third turn and in every pass after it, and the
destination is sector 0, the planned target the pass starts from. The gang in
slot 1 of player 1 is the exception: player 1 hired it in the second turn, so
its record still carries the `needs_family` flag when the probe writes 14, and
the dispatcher wipes the record and gives it family 0 before any handler runs
(RULE-AI-002). The write changes only the family and leaves the flag set. Before the
rebuild was corrected it called the selector in every scenario and refused the
modes of families 13 and 14 outside Big Man and Siege, so it could not replay
the run.

## Conclusion

The run agrees with RULE-AI-031 for a scenario without objectives: no selector
is called, and the gang moves to the planned target left from the start of the
pass, sector 0, every turn. The gangs therefore gather in sector 0.
