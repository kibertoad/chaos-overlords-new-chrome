---
id: EXP-TURN-033
title: Does a Bribe the player cannot pay for leave the cash and the base Tolerance unchanged?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-033.json
---

## Question

A Bribe pays 3 cash to raise its sector's base Tolerance by 3, and with less
than 3 cash it changes nothing (RULE-BRIBE-001). Earlier runs hold Bribes the
player could pay for. Does the original leave both the cash and the base
Tolerance alone when the player has 2?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 1` and ten Done presses (`--end-turns 10`),
with Bribe (2) given to the human's gang in roster slot 0 before every Done
press (`--orders 1:0:2:0:0:0,2:0:2:0:0:0,3:0:2:0:0:0,4:0:2:0:0:0,5:0:2:0:0:0,6:0:2:0:0:0,7:0:2:0:0:0,8:0:2:0:0:0,9:0:2:0:0:0,10:0:2:0:0:0`). The gang stays in
its starting sector 51, which the human owns, with base Income 6.

The seed was found in the rebuild, which played this plan for seeds 1 to 30;
in seed 1 the tenth Bribe meets 2 cash, and no computer player is given a Move
that DEV-AI-007 refuses.

## Observations

The run made 2493 calls of `roll`. At the end the human had 3 cash and
sector 51 had base Tolerance 29.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state. In its replay the human's cash at the
planning of turns 2 to 11 is 18, 16, 14, 12, 10, 8, 6, 4, 2 and 3: each turn
adds 1 and the first nine Bribes take 3, and the tenth, at 2 cash, fails. The
base Tolerance of 29 is 11 plus 3 for each of the nine paid Bribes, less the
nine one-point returns toward 11 after the first turn (RULE-TOLERANCE-001).

## Conclusion

The run agrees with RULE-BRIBE-001: nine Bribes each pay 3 and raise the base
Tolerance by 3, and a Bribe at 2 cash changes neither.
