---
id: EXP-TURN-057
title: What planning state do the computer players hold after thirty turns of Eliminate at Crimelord?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-057.json
---

## Question

EXP-TURN-053 compares the planning state of Eliminate at Goon. After thirty
turns at Crimelord, where the computer gangs take families 3, 7 and 10 to 12,
does the original hold the planning state the rebuild holds?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Eliminate (`--scenario 7`), Mentality 2
(`--mentality 2`), thirty Done presses (`--end-turns 30`) and
`--seed 7201`, with no orders. After the last press the probe reads the
planning state, the combat records and the combat result rows into the end
state, as in EXP-TURN-048.

## Observations

The run made 12518 calls of `roll`. At the end the planning records of
living gangs hold families 3 (22 records), 7 (7), 10 (5), 11 (8) and 12 (9).

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, and holds the same planning records, sector weights, per-player
values, combat records and, for each computer gang whose family is assigned,
focus and coverage sector.

## Conclusion

The run agrees with RULE-AI-022, RULE-AI-026, RULE-AI-028, RULE-AI-029 and
RULE-AI-030 for thirty turns of Eliminate.
