---
id: EXP-TURN-051
title: What do the combat records hold after an attack, its retaliation and a police kill?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-051.json
---

## Question

RULE-COMBAT-002 writes bytes 0 to 8 of the combat record (FMT-STATE-003) of
every gang that fought in a resolution, and RULE-POLICE-001 writes
`police_damage` of all 486 records. After a resolution with an attack that
draws a retaliation and a police attack that kills its gang, does the
original hold the values the rebuild's events give?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-011's first step: `--seed 61038 --end-turns 24 --orders
1:0:8:0:0:1,24:0:1:4:18:0`. The human's gang hides every turn and, in the
twenty-fourth, attacks gang 18 of player 4. After the last press the probe
reads every combat record that a resolution has written and the combat
result rows into the end state, with the planning state of EXP-TURN-048.

## Observations

The run made 12623 calls of `roll`. Three records hold values. Record 0, the
human's attacking gang: definition 0, `force_start` 10, `force_final` 6,
`damage_dealt` 0, `retaliation_taken` 4, no items, `police_damage` -1.
Record 342, its target: definition 33, Force 9 before and after, weapon 0 and
armor 26, `police_damage` -1, and 9 and -1 in the two bytes FND-COMBAT-008
calls undefined. Record 327, a gang of player 4 the police found:
`force_start` 1, `force_final` -6, `police_damage` 7. `force_shown` is 0
in all three, since Detailed Combat is switched off. The result row of sector
12 lists record 0 for player 0 with target 342 and record 342 for player 4
with target -1, and the row of sector 31 lists record 327 for player 4 with
`police_hit` 1 for player 4 (FMT-STATE-008).

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same state, and the records rebuilt from its attack and police events of the
last resolution hold the same definition, Forces, items, opening and
retaliation damage and police damage, and every combat result row matches.

## Conclusion

The run agrees with RULE-COMBAT-002 and RULE-POLICE-001 for the records they
write: `force_final` is not clamped at 0, `police_damage` holds the police's
successes beyond the gang's Force, and the target's record keeps the bytes
FND-COMBAT-008 calls undefined.
