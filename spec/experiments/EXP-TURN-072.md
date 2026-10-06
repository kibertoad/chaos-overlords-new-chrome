---
id: EXP-TURN-072
title: Is an item bought beside another player's completed Factory sold at full Cost, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-072.json
---

## Question

No other recorded run prices an item for a gang standing in a sector whose
Factory is complete and which another player owns. Is the price the full
Cost, as RULE-EQUIP-003 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 1 (`--mentality 1`),
thirty Done presses (`--end-turns 30`) and `--seed 2`. Before turn 29, write a
one-off Move (10) to sector 61 by roster slot 0; by then a computer player
owns sector 61 and its Factory is complete. Before turn 30, write a one-off
Equip (5) of item 40, a miscellaneous item of Cost 4 (EXP-TURN-031), by
roster slot 0; the discount would take 1 off its price
(`--orders 29:0:10:61:0:0,30:0:5:40:0:0`).

## Observations

The run made 13699 calls of `roll` over thirty Done presses. At the end
`elapsed_turns` is 30 and every player is still active. Sector 61 is owned by
player 4, and the human's gang, roster slot 0, stands in it carrying item 40
in its miscellaneous slot. The human's cash is 46.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, every player's cash included. In the
transaction pass of turn 30 the rebuild prices item 40 for a gang in a sector
whose completed Factory belongs to another player, and charges the full Cost
of 4. With the discount the human's cash would end at 47.

## Conclusion

The run agrees with RULE-EQUIP-003 for an item priced beside another player's
completed Factory, which gets no discount.
