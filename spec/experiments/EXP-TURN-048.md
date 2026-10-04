---
id: EXP-TURN-048
title: What planning state do the computer players hold after twenty-four turns of Greed at Crime Lord?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 3
fixture: EXP-TURN-048.json
---

## Question

RULE-AI-004 keeps a planning record per gang slot, an auxiliary record with a
focus and a coverage sector, a cached sector weight per player and sector, and
three per-player values: whether the player has planned, the raider mode and
the placement anchor (FMT-STATE-007). Greed is the one scenario whose
handlers turn gangs to Terminate. After twenty-four turns, does the original
hold the values the rebuild holds, byte for byte?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 2 (`--mentality 2`),
half a year (`--turns 26`) and twenty-four Done presses (`--end-turns 24`),
with no orders, once each with `--seed 4801`, `--seed 4802` and
`--seed 4803`. After the last press the probe reads the planning records,
the auxiliary focus and coverage values, the sector weights and the three
per-player values into the end state.

## Observations

The runs made 8102, 9192 and 9064 calls of `roll`. At the end of the three
runs 13, 14 and 12 planning records hold Terminate; 9, 12 and 10 of them keep
the target bytes of the Move or Attack that the Terminate replaced. Player 0,
the human, has not planned; players 1 to 5 have.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the runs. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, and holds the same planning record bytes, sector weights and
per-player values. For each computer gang whose family is assigned, it holds
the same focus and coverage sector.

## Conclusion

The runs agree with FND-AI-074: the Greed Terminate stores only the action
and the family flag, so the record keeps the targets an earlier write of the
same pass stored. The auxiliary values of a slot with no assigned family are
not compared, because the original leaves them 0 from the start of the match
and the rebuild holds -1 there until the slot's gang is assigned
(FND-AI-044).
