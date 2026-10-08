---
id: EXP-TURN-022
title: Does a family-3 gang attack a visible human gang at Mentality 3 as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-022.json
---

## Question

A family-3 gang attacks only when its previous action was Attack, Hide or Move
and its sector's weight is 10, a visible human gang whose player it holds in a
negative attitude (RULE-AI-004 `visible_weight`, RULE-AI-022). At Mentality 3
every computer player starts at -10 toward the human and stays there
(RULE-AI-014, RULE-AI-015). When the human's gang meets a family-3 gang that
has just moved, does the gang draw its target and attack as the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with the seed of EXP-TURN-013 (`--seed 46976`), Eliminate
(`--scenario 7`) and Mentality 3 (`--mentality 3`), pressing Done fifteen
times (`--end-turns 15`), with a Move (10) of the human's gang in roster slot
0 and 0 in `repeat_action` before twelve of the presses:

```
--orders 1:0:10:1:0:0,3:0:10:9:0:0,4:0:10:0:0:0,5:0:10:1:0:0,6:0:10:8:0:0,7:0:10:16:0:0,8:0:10:17:0:0,10:0:10:26:0:0,11:0:10:35:0:0,12:0:10:36:0:0,13:0:10:29:0:0,14:0:10:30:0:0
```

The route was found in the rebuild, which replays the seed: before each turn it
let the computer players plan on a copy of the match and moved the human's gang
into the sector a family-3 gang was about to move to.

The same run was made twice more with the same arguments: once with
`--trace-calls 0x00402D70`, which notes the arguments and result of every call
of the query function, and once with `--dump-at-roll 3465`, which copies the
data sections when the 3465th call of `roll` returns.

## Observations

The seed was 46976. The run made 3886 calls of `roll`, 315 before the first
Done press. In turn 15 the call at `0x0043661B` (family 3, FND-RNG-006),
`roll(1)` returning 1, was call 3464 counting from 0, the only one at that
address; call 3463 was a family-0 attack draw at `0x004295BA`, also `roll(1)`.
In the copy after the last turn player 0 had no living gang.

The traced run noted, after call 3464, the query call at `0x00436650` with the
arguments `[0x2B, 2, 30, 1]`, returning 0, and just before it the family-0
call at `0x004295EF` with `[0x2B, 2, 2, 1]`, returning 1. The human's gang was
in sector 30, where its last Move took it. In the dumped copy player 2's gang
record in slot 30 was all zero but its sector byte, 100. Player 3's record in
slot 1 had sector 100, Force -4, Combat 8, Defense 5 and a `visible_to` byte of
1 for player 2; no record of player 0 or 1 had sector 100 with that byte set.
No attack by player 2's family-3 gang was resolved in turn 15, and the human's
gang died in that turn's combat phase.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results and reaches the same generator position and state, with
the human's gang dead at the end of turn 15.

## Conclusion

The family-3 gang drew the human's gang, as RULE-AI-022 gives, and then planned
nothing, because the strength test was handed the sector, 30, as the roster
slot: it compared player 2's unused record 30 with player 3's dead gang in
slot 1, the first visible record with sector 100, and the test failed
(FND-AI-072, BUG-AI-007).
