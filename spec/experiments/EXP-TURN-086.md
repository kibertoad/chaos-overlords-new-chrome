---
id: EXP-TURN-086
title: Does a Dominance computer player force a hunter hire when a hostile human gang is in sight, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-086.json
---

## Question

No other recorded run reaches the hunter test of the Dominance hire roles.
When a hostile human gang is visible where no hunter covers it and the other
conditions hold, does the role become slot 10, as RULE-AI-010 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Dominance (`--scenario 3`), Mentality 3 (`--mentality
3`), seventeen Done presses (`--end-turns 17`) and `--seed 11`. The human
gives no orders.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 12, with families 0, 4, 5 and 7 written into every
computer gang at turn 3, 8 or 14 and with no write, and recorded which of a
list of unreached planner branches each match took.

## Observations

The run made 6261 calls of `roll` over seventeen Done presses. At the end
`elapsed_turns` is 17 and no player's `controller` has changed.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, the planning records included. The
rebuild reaches the Dominance hunter test that sets the hire role to slot 10.

## Conclusion

The run agrees with RULE-AI-010 for the forced Dominance hunter role.
