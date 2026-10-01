---
id: EXP-TURN-032
title: Does a Snitch that takes a base Tolerance below 1 leave it at 1?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-032.json
---

## Question

A Snitch lowers its sector's base Tolerance by 3 whatever the player's cash
(RULE-SNITCH-001), and after the instant phase every base Tolerance below 1 is
set to 1 (RULE-TOLERANCE-002). No earlier run takes a base Tolerance below 1.
Does the original clamp it?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 1` and six Done presses (`--end-turns 6`), with
Snitch (13) given to the human's gang in roster slot 0 before every Done
press (`--orders 1:0:13:0:0:0,2:0:13:0:0:0,3:0:13:0:0:0,4:0:13:0:0:0,5:0:13:0:0:0,6:0:13:0:0:0`).
Snitch cannot be a recurring order, so it is given each turn. The gang stays
in its starting sector 51, which the human owns, with base Income 6.

The seed was found in the rebuild, which played this plan for seeds 1 to 30;
in seed 1 the base Tolerance falls below 1 in turns 5 and 6, and no computer
player is given a Move that DEV-AI-007 refuses.

## Observations

The run made 1140 calls of `roll`. At the end sector 51 had base Tolerance 1
and the human had 26 cash.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state. In its replay the sector's base Tolerance
at the planning of turns 1 to 7 is 11, 8, 6, 4, 2, 1 and 1: each resolution
moves it one point up toward 17 - 6 = 11 (RULE-TOLERANCE-001) and the Snitch
takes 3 off. In turn 5 that makes 0 and in turn 6 -1, and both end at 1.

## Conclusion

The run agrees with RULE-SNITCH-001 and with the lower bound of
RULE-TOLERANCE-002. No run takes a base Tolerance above 40.
