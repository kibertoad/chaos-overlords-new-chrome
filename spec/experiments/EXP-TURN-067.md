---
id: EXP-TURN-067
title: Does the turn start drop a recurring Heal at Force 10 and the recurring orders of dead gangs, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-067.json
---

## Question

No other recorded run gives a gang a recurring Heal that reaches Force 10, or
loses a gang that holds a recurring order. Does the turn start drop both, as
RULE-TURN-004 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
twelve Done presses (`--end-turns 12`) and `--seed 10`, with these orders,
each written into `action` and, for a recurring order, `repeat_action` of the
human's gang (FMT-STATE-001):

- before turn 1, Chaos (3) by roster slot 0, recurring;
- before turn 2, Chaos (3) by roster slot 1, recurring;
- before turn 3, Influence (9) of site slot 1 by roster slot 2, recurring;
- before turn 4, Heal (7) by roster slot 3, recurring

(`--orders 1:0:3:0:0:1,2:1:3:0:0:1,3:2:9:1:0:1,4:3:7:0:0:1`). In turns 1, 2
and 3 write a hire order for offer slot 0 into sector 54
(`--hires 1:0:54,2:0:54,3:0:54`).

## Observations

The run made 2562 calls of `roll` over twelve Done presses. At the end
`elapsed_turns` is 12 and every player is still active. The human has lost
three gangs (`casualties` 3), two of them holding a recurring Chaos, in
turns 7 and 8, and holds -40 cash. The healing gang reached Force 10 and then
made no more Heal rolls.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, and at the end each surviving human
gang's recurring order matches the original's `repeat_action`. A Heal carried
on at Force 10 would have rolled dice (FND-HEAL-001) and changed the calls, and
a dead gang's Chaos carried on would have rolled for a gang no longer there.

## Conclusion

The run agrees with RULE-TURN-004 for a recurring Heal dropped once the gang is
at Force 10 and for the recurring orders of gangs killed in Combat.
