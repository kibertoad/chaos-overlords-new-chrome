---
id: EXP-TURN-065
title: Do the human's recurring Research, Influence and Control orders end when they are done, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-065.json
---

## Question

No other recorded run gives the human a recurring order that the turn start
clears. When the human's gangs hold a recurring Research, Influence and
Control, does each end once its item is researched, its site complete or its
sector won, as RULE-TURN-004 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
twelve Done presses (`--end-turns 12`) and `--seed 4`, with these orders,
each written into `action` and, for a recurring order, `repeat_action` of the
human's gang (FMT-STATE-001):

- before turn 1, Research (11) of item 2 by roster slot 0, recurring;
- before turn 2, Influence (9) of site slot 1 by roster slot 1, recurring;
- before turn 3, Move (10) to sector 25 by roster slot 2, once;
- before turn 4, Control (4) by roster slot 2, recurring

(`--orders 1:0:11:2:0:1,2:1:9:1:0:1,3:2:10:25:0:0,4:2:4:0:0:1`). In turns 1
and 2 write a hire order for offer slot 0 into sector 33, the headquarters
sector (`--hires 1:0:33,2:0:33`), so roster slots 1 and 2 hold gangs there.

## Observations

The run made 2219 calls of `roll` over twelve Done presses. At the end
`elapsed_turns` is 12 and every player is still active. The human owns sector
25, and site slot 1 of sector 33 holds progress 8. The three gangs hold 0 in
`action` and `repeat_action`; their `target` bytes still hold 2, 1 and 0.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, and at the end each human gang's
recurring order matches the original's `repeat_action`. A Research or
Influence carried on after it was done would have rolled dice and changed the
calls.

## Conclusion

The run agrees with RULE-TURN-004 for a recurring Research cleared once the
item is researched, a recurring Influence cleared once the site is complete
and a recurring Control cleared once the sector is the player's, and with its
edge case that `repeat_target` stays in `target`.
