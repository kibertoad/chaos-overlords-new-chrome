---
id: EXP-ATTACK-002
title: Which of an opponent's gangs in the sector does the Attack picker list when the player sees only some of them?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-ATTACK-002.json
---

## Question

When an opponent has several gangs in the acting gang's sector and the player
sees all but one, does the picker list the seen ones in roster order?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-093, with `--attack-lists` added. The lists are read as in
EXP-ATTACK-001.

## Observations

The run made the same 18860 calls of `roll` with the same bounds and results
as EXP-TURN-093, and its events are the same. Every value of EXP-TURN-093's
end state is the same except `damage_dealt` of combat records 85, 91 and 92
(FMT-STATE-003), records that the replay does not compare because their gangs
did not attack in the last resolution (FND-COMBAT-008).

The human has one gang, in sector 54. Player 5 has five gangs there, in
roster slots 5, 6, 9, 12 and 27; the human's `visible_to` entry is 0 for slot
5 and 1 for the others. The list for player 5 holds 6, 9, 12 and 27, and the
lists for the other players are empty.

## Results

A test of the rebuild compares the lists as in EXP-ATTACK-001, and they are the
same, in the same order.

## Conclusion

The run agrees with RULE-ATTACK-002: the picker lists the seen gangs in roster
order and leaves out the one the player does not see.
