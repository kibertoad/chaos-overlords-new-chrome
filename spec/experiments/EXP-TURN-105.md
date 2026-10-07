---
id: EXP-TURN-105
title: Do the Greed Terminate branches of the family 1, 5, 6 and 12 handlers flag the record for a new family?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-105.json
---
## Question

In the last turns of a Greed match the planning handlers of seven families
replace the planned action with Terminate and set `needs_family` in the
record (FMT-STATE-007, FND-AI-042). EXP-TURN-048 reaches the branches of the
family 2, 3 and 7 handlers; no Greed run has a gang of family 1, 5, 6 or 12 in
those turns. Do their branches do the same?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-048 with `--seed 4801` and twenty-four Done presses, and with the
probe writing family 1, 5, 6 and 12 into the planning records of roster slot 0
of players 1, 2, 3 and 4 before the Done press of turn 23
(`--scenario 0 --mentality 2 --turns 26 --seed 4801 --end-turns 24
--families 23:1:0:1,23:2:0:5,23:3:0:6,23:4:0:12`). After the last press the
probe reads the planning records into the end state, as for EXP-TURN-048.

The plan was found in the rebuild, which played the three seeds of
EXP-TURN-048 with the writes before turns 22, 23 and 24, and reached the four
branches in every one.

## Observations

The run made 7953 calls of `roll`, with the Done presses at the counts listed
in the fixture.

## Results

A test of the rebuild replays the run with
the same family writes. The rebuild makes the same calls with the same bounds
and results and reaches the same state, and every byte of every planning
record matches the original's. In its replay the planning pass of turn 24
reaches the Greed Terminate branch for the four written records, families 1,
5, 6 and 12, as well as for gangs of families 2, 3 and 7, and each sets the
planned action to Terminate and `needs_family` to 1.

## Conclusion

The run agrees with FMT-STATE-007 and FND-AI-042 for the Greed Terminate
branches of the family 1, 5, 6 and 12 handlers.
