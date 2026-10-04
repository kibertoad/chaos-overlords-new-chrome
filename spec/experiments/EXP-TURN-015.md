---
id: EXP-TURN-015
title: Do a human gang's Equip and Sell orders, and eight turns of the computer players, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-015.json
---

## Question

When the human's gang equips a weapon, an armor and a miscellaneous item in
turns 1 to 3 and sells all three at once in turn 4, do the prices and the
Sell payout reach the cash the spec gives, and do the computer players' eight
turns draw and resolve as in the earlier runs?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-010, in Kill 'Em All (scenario 4), pressing Done eight times,
   with five orders for the human's gang in roster slot 0, each written before
   the Done press of its turn with 0 in `repeat_action` unless given: Equip (5)
   with item 12 in turn 1, item 24 in turn 2 and item 39 in turn 3, Sell (12)
   with the mask 7 in turn 4, and Hide (8) with Hide in `repeat_action` in
   turn 5
   (`--end-turns 8 --orders 1:0:5:12:0:0,2:0:5:24:0:0,3:0:5:39:0:0,4:0:12:7:0:0,5:0:8:0:0:1`).
2. Repeat the run with its seed (`--seed 24133`), copying the writable
   sections at the entry of call 784 of `roll`, counting from 0; again at the
   entries of calls 775, 945 and 950, one repetition each, the last with every
   call of the sector selector `0x00408642` noted (`--trace-calls 0x00408642`).

## Observations

The seed was 24133. The run made 1647 calls of `roll`, 327 before the first
Done press. Items 12, 24 and 39 are a weapon (type 2, Cost 3), an armor (type
3, Cost 2) and a miscellaneous item (type 4, Cost 3). At the end the human's
gang held no item and had Hide in `action` and `repeat_action`.

In turn 5, player 5's gang in roster slot 2, of family 0, stood in sector 9.
The selector call for it from `0x0042A467`, with mode 5, made call 784,
`roll(9)`, which gave 4, and returned sector 5. In the copy at that call the
first nine pairs of `selector_pairs` held score 5 with sectors 0, 1, 2, 5, 6,
8, 16, 17 and 18. Sectors 5 and 6 are more than one step from sector 9; all
nine were neutral and none under police. In the copy at call 945 the gang's
planning record held Move to sector 5 as its planned action, and the gang
stood in sector 5. In the copies at calls 775 and 945 every other gang stood
where the rebuild has it.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run,
giving the Sell as one command for the three items, with DEV-AI-007 switched
off. The rebuild makes the same calls with the same bounds and results, moves
player 5's gang to sector 5 in turn 5, and reaches the same state. With
DEV-AI-007 on, it refuses that Move, so the gang stays in sector 9, and at call
950 its tie count for that player is 8 where the original's is 7.
EXP-TURN-016 repeats the first four turns, which end before that Move.

## Conclusion

The run shows that the sector selector can return a sector more than one step
away, when the first pair after the sort is a neighbour and a pair kept from
an earlier call ties with it (RULE-AI-006), and that the Move pass then puts
the gang in that sector (RULE-MOVE-001). Up to that Move it agrees with the
spec; the Equips and the Sell are compared in EXP-TURN-016.
