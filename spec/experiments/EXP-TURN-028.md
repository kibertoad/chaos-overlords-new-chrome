---
id: EXP-TURN-028
title: Do the computer players stop hiring in the closing turns of a six-month Greed?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-028.json
---

## Question

In Greed a computer player tries to hire only while more turns remain than an
eighth of the turn limit (RULE-AI-011). Earlier runs end long before the
closing turns of their scenario, so they reach only the gang limit. Does the
original stop the hires where the rule gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 1001`, Greed (`--scenario 0`), six months
(`--turns 26`) and 25 Done presses (`--end-turns 25`), with no orders.

The seed was found in the rebuild, which played this setup for seeds 1000 to
1200: in most of them the closing-turn test refuses a hire in turns 24 and 25,
and seed 1001 is the first in which no computer player is given a Move that
DEV-AI-007 refuses.

## Observations

The run made 10081 calls of `roll`, and the human was still in play after the
25th Done press.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state. In the rebuild's replay the hire gate is
tested 125 times: 72 pass, 45 fail on the gang limit, and 8 fail in the
closing turns, where 26 / 8 = 3 turns or fewer remain.

## Conclusion

The run agrees with RULE-AI-011 for Greed's closing turns and its gang limit of
one and a half times the owned sectors. No recorded run reaches the limit that
applies once no neutral sector is free, which depends on cash.
