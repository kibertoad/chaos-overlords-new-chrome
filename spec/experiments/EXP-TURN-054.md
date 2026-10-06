---
id: EXP-TURN-054
title: What focus does a family-3 gang hold after planning Influence in its first case?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-054.json
---

## Question

FND-AI-076 reads the family-3 handler as storing -1 in the focus after
every action it plans when the gang's previous action was None, Control,
Equip or Heal. After seven turns of Armageddon, does the original hold the
focus values the rebuild holds?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Armageddon (`--scenario 9`), Mentality 3
(`--mentality 3`), seven Done presses (`--end-turns 7`) and `--seed 9301`,
with no orders. After the last press the probe reads the planning state, the
combat records and the combat result rows into the end state, as in
EXP-TURN-048.

## Observations

The run made 1400 calls of `roll`. At the end, record 411, a family-3 gang
of player 5 in sector 51 whose previous action is None, has Influence on
site slot 2 planned and holds -1 in its focus and its coverage sector.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, and holds the same planning records, sector weights, per-player
values and, for each computer gang whose family is assigned, focus and
coverage sector.

## Conclusion

The run agrees with FND-AI-076. A rebuild whose family 3 keeps the gang's
sector as the focus of that Influence holds 51 in record 411.
