---
id: EXP-TURN-035
title: Does a hire the player could afford at planning fail, with a cash report, once an Equip has spent the cash?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-035.json
---

## Question

The hire screen does not take the hire cost (RULE-HIRE-003); the hire phase
pays it from the cash left after the turn's transactions, and a hire the
player can no longer pay for fails with a cash report (RULE-HIRE-001,
RULE-EVENT-009). Does the original refuse a hire that an Equip of the same
turn left unaffordable, and what report does it leave?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 3` and two Done presses (`--end-turns 2`). In
turn 1 the human's gang bribes. In turn 2, with 18 cash, it equips item 40, a
miscellaneous item of Cost 4, and the probe writes offer slot 1's hire order
with sector 54, the gang's sector; the offer is gang definition 88, whose hire
cost is 15 (`--orders 1:0:2:0:0:0,2:0:5:40:0:0 --hires 2:1:54`).

The seed was found in the rebuild, which played this plan for seeds 1 to 40.
Seed 3 reaches such a turn earliest, with no computer player given a Move that
DEV-AI-007 refuses.

## Observations

The run made 530 calls of `roll`. At the end the human had 15 cash and one
gang, and one Last Turn report: type 6 (cash short) with `arg1` 4 (hire),
`arg2` 88 and `arg3` 0.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same generator position and state, and builds the same Last Turn reports for
every player. In its replay the Equip takes the cash from 18 to 14, the hire of
cost 15 fails, and the next turn's income brings the cash to 15.

## Conclusion

The run agrees with RULE-HIRE-001 and RULE-EVENT-009.
