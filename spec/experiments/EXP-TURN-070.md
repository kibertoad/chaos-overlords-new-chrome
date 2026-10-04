---
id: EXP-TURN-070
title: Does the turn start drop a recurring Control in a sector under police presence, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-070.json
---

## Question

No other recorded run holds a recurring Control in a sector that has police
presence and that the player does not own. Does the turn start drop it, as
RULE-TURN-004 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
seven Done presses (`--end-turns 7`) and `--seed 24`. In turns 1 and 2 write a
hire order for offer slot 0 into sector 33, the headquarters sector
(`--hires 1:0:33,2:0:33`); the gang hired in turn 1, roster slot 1, has a
Force plus Control below the Income of sector 26. With these orders, each
written into `action` and, for a recurring order, `repeat_action` of the
human's gang (FMT-STATE-001):

- before turn 3, Move (10) to sector 26, a neutral neighbour, by roster
  slots 0, 1 and 2, once;
- before turn 4, Chaos (3) by roster slots 0 and 2 and Control (4) by roster
  slot 1, all recurring

(`--orders 3:0:10:26:0:0,3:1:10:26:0:0,3:2:10:26:0:0,4:0:3:0:0:1,4:1:4:0:0:1,4:2:3:0:0:1`).

## Observations

The run made 1316 calls of `roll` over seven Done presses. At the end
`elapsed_turns` is 7 and every player is still active.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, and at the end each human gang's
recurring order matches the original's `repeat_action`. The Control of roster
slot 1 fails each turn until the Chaos of the other two brings a Crackdown to
sector 26, and the next turn start drops it.

## Conclusion

The run agrees with RULE-TURN-004 for a recurring Control dropped because its
sector is under police presence.
