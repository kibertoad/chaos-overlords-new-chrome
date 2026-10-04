---
id: EXP-TURN-068
title: Does a Heal with a pool of 0 or less roll nothing, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-068.json
---

## Question

No other recorded run orders a Heal by a gang whose effective Heal is -4 or
less. Does such a Heal roll no dice, as RULE-HEAL-001 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
three Done presses (`--end-turns 3`) and `--seed 23`. In turn 1 write a hire
order for offer slot 0 into sector 51 (`--hires 1:0:51`); the offer holds a
gang whose effective Heal there is -4 or less (FMT-DATA-002). Before turn 2,
write a one-off Heal (7) by roster slot 1, the hired gang, into its `action`
(`--orders 2:1:7:0:0:0`).

## Observations

The run made 552 calls of `roll` over three Done presses. At the end
`elapsed_turns` is 3 and every player is still active. The hired gang's Force
is unchanged by the Heal.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state. A Heal that rolled dice for a pool of
0 or less would have added calls of `roll(6)` in turn 2.

## Conclusion

The run agrees with RULE-HEAL-001 for a Heal pool of 0 or less, which rolls
nothing and leaves Force unchanged.
