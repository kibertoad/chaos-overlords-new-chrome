---
id: EXP-TURN-017
title: Do thirty turns of a new local Kill 'Em All game, up to the human's elimination, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-017.json
---

## Question

As EXP-TURN-010, over more turns: do the turns past the twenty-fifth, up to
the one in which computer players' gangs kill the human's hiding gang, make
the draws and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-010, pressing Done fifty-two times (`--end-turns 52`), with
   Hide (8) in `action` and `repeat_action` of the human's gang in roster
   slot 0 before the first press (`--orders 1:0:8:0:0:1`).
2. Repeat the run with its seed over thirty turns
   (`--seed 44213 --end-turns 30`).

## Observations

The seed was 44213. In the first run the thirty-first Done press, after call
19128 of `roll`, reached no planning phase, and the probe stopped with no state
copied. The repetition over thirty turns made the same 19128 calls, 330 before
the first Done press, and copied the state after the thirtieth turn. In it
player 0's `controller` was -2 and its only gang's record inactive. The
thirtieth turn made 1258 calls: four at `0x00473ABC`, the Hide test of an
attack, and one each at `0x0042A1AB`, `0x00434AFD` and `0x00431D4F`, the
attack target draws of families 0, 1 and 6.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run up to
the planning phase that follows the human's elimination. The rebuild makes the
same calls with the same bounds and results and reaches the same generator
position and state. In it four gangs of player 2 attack the human's gang in
sector 12, which hides: three draws fall below the evasion threshold and miss,
and the fourth attack hits and takes the gang's Force from 4 to 0, which
eliminates the human.

## Conclusion

The run agrees with the spec over thirty turns of Kill 'Em All, including
computer players' attacks on a hiding gang (RULE-ATTACK-001) and a death in
combat (RULE-COMBAT-002, RULE-GANG-002).
