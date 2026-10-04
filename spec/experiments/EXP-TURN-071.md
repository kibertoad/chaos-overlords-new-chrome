---
id: EXP-TURN-071
title: Does the turn start drop a recurring Control under police presence in a Criminal game, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-071.json
---

## Question

Does the case of EXP-TURN-070 hold at Mentality 1, where every computer
player resolves at band 1?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-070 with Mentality 1 (`--mentality 1`) and `--seed 16`, hires
into sector 12, the headquarters sector (`--hires 1:0:12,2:0:12`), and the
same orders with sector 19 as the neutral neighbour
(`--orders 3:0:10:19:0:0,3:1:10:19:0:0,3:2:10:19:0:0,4:0:3:0:0:1,4:1:4:0:0:1,4:2:3:0:0:1`).

## Observations

The run made 1452 calls of `roll` over seven Done presses. At the end
`elapsed_turns` is 7, every player is still active and every player's
`difficulty_band` is 1.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, and at the end each human gang's
recurring order matches the original's `repeat_action`.

## Conclusion

The run agrees with RULE-TURN-004 for a recurring Control dropped because its
sector is under police presence.
