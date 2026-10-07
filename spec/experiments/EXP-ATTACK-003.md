---
id: EXP-ATTACK-003
title: Which gangs does the Attack picker list when several opponents share the acting gang's sector?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-ATTACK-003.json
---

## Question

When gangs of three opponents stand in the acting gang's sector, does each
opponent's list hold only that opponent's gangs?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-026, with `--equip-lists` and `--attack-lists` added
(`--scenario 9 --mentality 1 --seed 5490 --end-turns 28 --equip-lists --attack-lists`).
The lists are read as in EXP-ATTACK-001, and the Equip lists as in
EXP-EQUIP-001.

## Observations

The run made the same 14892 calls of `roll` with the same bounds and results
as EXP-TURN-026, and every value of EXP-TURN-026's end state is the same; the
end state also holds fields recorded after EXP-TURN-026 was. Its Equip lists
are those of EXP-EQUIP-003.

The human has one gang, in sector 33. Player 2 has four gangs there, in roster
slots 22, 23, 25 and 26, player 4 one in slot 1 and player 5 one in slot 25,
and the human sees all six. The lists for players 2, 4 and 5 hold exactly
those slots in that order, and the lists for players 1 and 3 are empty.

## Results

A test of the rebuild compares the lists as in EXP-ATTACK-001, and they are the
same. Another test compares the Equip lists as in EXP-EQUIP-003.

## Conclusion

The run agrees with RULE-ATTACK-002 for gangs of several opponents in one
sector.
