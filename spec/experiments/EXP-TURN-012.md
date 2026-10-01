---
id: EXP-TURN-012
title: Do twenty-three turns of a new local Siege game draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-012.json
---

## Question

As EXP-TURN-010, in Siege (scenario 6), where the computer players' hire roles
give families 13 and 14 their objective sectors: do the turns make the draws
and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-010, with Siege chosen on the setup screen (`--scenario 6`),
   pressing Done twenty-five times, with Hide (8) in `action` and
   `repeat_action` of the human's gang in roster slot 0 before the first press
   (`--orders 1:0:8:0:0:1`).
2. Start the run again with its recorded seed and twenty-three turns
   (`--seed 11541 --end-turns 23`).

## Observations

In the first attempt, with seed 11541, the twenty-fifth Done press was not
taken: after 10639 calls of `roll` the planning phase of turn 25 never came,
and the probe stopped with no state copied. The repetition over twenty-three
turns made the same calls and stopped at the planning phase of turn 24, after
9778 calls, 313 of them before the first Done press. The Last Turn Events
panel opened once, after call 6533. The copy held 23 in `elapsed_turns`, every
player active, and the human's gang in roster slot 0.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state.

## Conclusion

The run agrees with the spec over twenty-three turns of Siege. Why the
twenty-fifth Done press of the first attempt was not taken is not recorded.
