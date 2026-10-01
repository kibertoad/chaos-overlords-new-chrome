---
id: EXP-TURN-027
title: Does a human's Give of two items to a gang hired the turn before reach the state the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-027.json
---

## Question

No earlier run holds a Give or a hire by the human. When the human's gang buys
a weapon and an armor, the human hires a second gang into the same sector, and
the first gang gives both items to it, do the hire and the Give reach the state
the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-016, with its seed and three turns. In turn 1 the human's gang
equips item 12 and in turn 2 item 24, as in EXP-TURN-016. After the turn 2
order the probe writes offer slot 1's hire order with sector 33, the gang's
sector, into `hire_orders` as the hire screen does (RULE-HIRE-003). In turn 3
the gang gives with mask 3, weapon and armor, to roster slot 1
(`--seed 24133 --end-turns 3 --orders 1:0:5:12:0:0,2:0:5:24:0:0,3:0:6:3:1:0 --hires 2:1:33`).

A fourth turn was left out: in it player 5's gang in sector 9 is given Move to
sector 5, which the original carries out and the rebuild refuses by
DEV-AI-007, as in turn 5 of EXP-TURN-015.

## Observations

The run made 649 calls of `roll`. At the end the human's roster slot 1 held a
gang of definition 36 in sector 33 with Force 8, weapon 12 and armor 24, and
the gang in slot 0 held no item.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run,
giving the hire as a hire of the gang offer slot 1 holds and the Give as one
command for the recipient and the two items. The rebuild makes the same calls
with the same bounds and results and reaches the same generator position and
state.

## Conclusion

The run agrees with RULE-HIRE-001 for a human's hire and with RULE-GIVE-001:
the giver's two slots are emptied and the recipient holds both items at the
end of the turn.
