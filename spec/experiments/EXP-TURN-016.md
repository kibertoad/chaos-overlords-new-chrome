---
id: EXP-TURN-016
title: Do a human gang's Equip orders and one Sell of three items pay and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-016.json
---

## Question

EXP-TURN-015 departs from the rebuild in turn 5 by DEV-AI-007. Over the first
four turns of the same match, which hold the human's three Equips and the
Sell, do the prices and the Sell payout reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-015, with its seed and four turns and without the turn 5 order
(`--seed 24133 --end-turns 4 --orders 1:0:5:12:0:0,2:0:5:24:0:0,3:0:5:39:0:0,4:0:12:7:0:0`).

## Observations

The run made 774 calls of `roll`, the same calls as the first 774 of
EXP-TURN-015. At the end the human's gang held no item.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run,
giving the Sell as one command for the three items. The rebuild makes the same
calls with the same bounds and results and reaches the same generator position
and state. In it the three Equips cost 3, 2 and 3, and the Sell pays 1, half
the Cost of the miscellaneous item, where half the Cost of each of the three
items would make 3.

## Conclusion

The run agrees with RULE-EQUIP-001 and with RULE-SELL-001 and BUG-SELL-001:
selling several items pays half the Cost of the last selected slot only.
