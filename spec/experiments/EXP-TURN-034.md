---
id: EXP-TURN-034
title: What Last Turn report does a human's Equip one short of its price leave?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-034.json
---

## Question

RULE-EVENT-014 records a cash report for an Equip the player cannot pay for,
with the gang's sector and definition. EXP-TURN-031 holds the human's failed
Equips in turns 7 and 8, but its state is read after turn 9, whose reports
replace them (RULE-EVENT-001). What report does the resolution of turn 8
leave?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-031 without the turn 9 orders and with eight Done presses
(`--seed 1 --end-turns 8 --orders 1:0:5:24:0:0,2:1:5:24:0:0,3:0:2:0:0:0,4:0:2:0:0:0,5:0:2:0:0:0,6:1:5:40:0:0,7:1:5:0:0:0,8:0:5:0:0:0,8:1:12:2:0:0 --hires 1:0:51`).
In turn 8 the human's Right Hands in roster slot 0, in sector 51, order item 0
with 0 cash, and the hired gang in slot 1 sells its armor.

## Observations

The run made 1613 calls of `roll`, the first 1613 of EXP-TURN-031. At the
end the human held one Last Turn report: type 6 (cash short) with `arg1` 2
(Equip), `arg2` 51 and `arg3` 0, the Right Hands' definition.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same generator position and state, and builds the same Last Turn reports for
every player.

## Conclusion

The run agrees with RULE-EVENT-014.
