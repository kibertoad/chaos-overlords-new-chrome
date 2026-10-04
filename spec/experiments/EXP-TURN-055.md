---
id: EXP-TURN-055
title: Does a family-2 Equip in a hostile human's sector survive the late Control gates in Big 40?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-055.json
---

## Question

FND-AI-077 reads family 2's late Control gates as testing the sector
numbered like the item when the gang plans an Equip. Over twenty-two turns of
Big 40 at Homicidal Maniac, does the original keep the Equips and Controls
that reading gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Big 40 (`--scenario 5`), Mentality 3
(`--mentality 3`), twenty-two Done presses (`--end-turns 22`) and
`--seed 5301`, with no orders. After the last press the probe reads the
planning state, the combat records and the combat result rows into the end
state, as in EXP-TURN-048. Two further runs with the same settings stopped
at roll 4612 and roll 5276 (`--dump-at-roll`) and read the planning records,
the attitudes and the player-pair records there.

## Observations

The run made 11030 calls of `roll`. In the pass after the fifteenth Done
press, player 4's family-2 gang in roster slot 9 stands in sector 51, owned
by the human, whose only gang there is hidden from player 4. At roll 4612,
inside player 4's pass, player 4's attitude toward the human is -10 and its
combat-advantage flag toward the human is 1, and the gang has Equip of item
3 planned. Sector 3 belongs to player 1, toward whom player 4's attitude is
10. At roll 5276, in the next pass, the gang's previous action is that Equip
and it plans Control.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, and holds the same planning records, sector weights, per-player
values and, for each computer gang whose family is assigned, focus and
coverage sector.

## Conclusion

The run agrees with FND-AI-077 and BUG-AI-008. A rebuild whose gates read
the gang's sector replaces the Equip with Control in the fifteenth pass,
and from the next pass its draws differ: the original refills a hire offer
at roll 5275 where that rebuild makes a sector selector draw.
