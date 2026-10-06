---
id: EXP-TURN-053
title: What planning state do family-11 and family-12 gangs leave after forty turns of Eliminate?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-053.json
---

## Question

FND-AI-075 reads the family-12 handler as storing the focus with every action
it plans and a Move's destination as the coverage sector. After forty turns of
Eliminate, whose computer gangs take families 10 to 14, does the original hold
the focus and coverage values the rebuild holds?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Eliminate (`--scenario 7`), Mentality 0
(`--mentality 0`), forty Done presses (`--end-turns 40`) and
`--seed 7001`, with no orders. After the last press the probe reads the
planning state, the combat records and the combat result rows into the end
state, as in EXP-TURN-048.

## Observations

The run made 12874 calls of `roll`. At the end, family-12 gangs hold the
destinations of their last Moves as their coverage sectors, for example 1 in
records 105 and 113, where the gangs were hired in sector 8, and record 406
holds 3 in both values. Family-11 gangs in records 93, 95 and 325 and a
family-0 gang in record 337 hold coverage sectors that differ from the sector
they were hired in.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, and holds the same planning records, sector weights, per-player
values and, for each computer gang whose family is assigned, focus and
coverage sector.

## Conclusion

The run agrees with FND-AI-075. The family-11 and family-0 coverage sectors
are those the gangs stored as family 12 before a later pass gave them another
family. A rebuild whose family 12 stores neither value differs in ten values:
the coverage sectors of family-12, family-11 and family-0 gangs, those above
among them, and one family-12 focus.
