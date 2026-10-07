---
id: EXP-TURN-020
title: Do the family-5 and family-7 attacks on a human gang that walks into computer land draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-020.json
---

## Question

A family-5 or family-7 gang attacks only a hostile gang it sees in its own
sector. When the human's gang walks through computer-owned land, do those
attacks draw their target and resolve as the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with the seed of EXP-TURN-018 (`--seed 36429`), pressing
Done twenty-four times (`--end-turns 24`), with a Move (10) of the human's
gang in roster slot 0 and 0 in `repeat_action` before eleven of the presses:

```
--orders 1:0:10:40:0:0,3:0:10:48:0:0,6:0:10:56:0:0,7:0:10:48:0:0,9:0:10:49:0:0,10:0:10:50:0:0,11:0:10:58:0:0,12:0:10:51:0:0,14:0:10:44:0:0,15:0:10:43:0:0,19:0:10:35:0:0
```

The route was found by playing Moves to random neighbours in the rebuild,
which replays the seed, until a family-5 gang planned an Attack.

## Observations

The seed was 36429. The run made 11334 calls of `roll`, 318 before the first
Done press. After the twenty-fourth press the calls at `0x0043AC35` (family 5)
and `0x00436D55` (family 7), both `roll(1)` returning 1, were calls 10547
and 10549; no earlier call was made at either. In the copy after the last turn
player 0 had no living gang.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results and reaches the same generator position and state, with
the human's gang dead at the end of turn 24.

## Conclusion

The run agrees with the attack branches of RULE-AI-024 and RULE-AI-026: each
gang draws once from a pool holding the human's gang alone and plans the
Attack.
