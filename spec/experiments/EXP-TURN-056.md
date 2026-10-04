---
id: EXP-TURN-056
title: Does a family-2 Equip in a hostile human's sector survive the late Control gates in Acceptance?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-056.json
---

## Question

FND-AI-077 reads family 2's late Control gates as testing the sector
numbered like the item when the gang plans an Equip. Over twenty-eight turns
of Acceptance at Crimelord, does the original keep the Equips that reading
gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Acceptance (`--scenario 2`), Mentality 2
(`--mentality 2`), a turn limit of 104 (`--turns 104`), twenty-eight Done
presses (`--end-turns 28`) and `--seed 2201`, with no orders. After the last
press the probe reads the planning state, the combat records and the combat
result rows into the end state, as in EXP-TURN-048.

## Observations

The run made 14570 calls of `roll`. In the pass after the twenty-fourth
Done press, player 1's family-2 gang in roster slot 7 plans Equip of item 26
in sector 30, which the human owns and toward whom player 1 is hostile.
Sector 26 belongs to player 4.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, and holds the same planning records, sector weights, per-player
values and, for each computer gang whose family is assigned, focus and
coverage sector.

## Conclusion

The run agrees with FND-AI-077 and BUG-AI-008. A rebuild whose gates read
the gang's sector replaces that Equip with Control, and its draws differ
from roll 11305 on.
