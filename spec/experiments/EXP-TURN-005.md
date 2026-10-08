---
id: EXP-TURN-005
title: Do three turns of new local Greed games, each ended with no orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-005.json
---

## Question

As EXP-TURN-001, over three turns of scenario 0: do the Greed branches of the
computer players' hiring and planning make the draws and reach the state the
spec gives?

## Setup

As EXP-TURN-001. Before pressing Begin the probe wrote 0 into the scenario
dword `0x004ABBE8` and its preference byte `0x00487858` (FND-SETUP-013).
`turn_limit` and `mentality` were left as the setup screen set them.

## Procedure

As EXP-TURN-002, pressing Done three times (`--scenario 0 --end-turns 3`).

## Observations

The seeds were 44840 and 15273. Begin made 315 and 310 calls of `roll`. The
three turns made 99, 98 and 82 more in the first run, and 107, 102 and 94 in
the second:

| Calls by turn, first run | Calls by turn, second run | Call instruction | Bound |
|---|---|---|---|
| 15, 5, 0 | 16, 5, 0 | `0x0047172A` | 89 |
| 0, 2, 3 | 0, 3, 0 | `0x00409C24` | 8 |
| 5, 0, 0 | 5, 0, 0 | `0x00475AC6` | 5 |
| 79, 91, 79 | 86, 94, 94 | `0x00475FBB` | 6 |

The copies held 0 in `scenario`, 52 in `turn_limit` and 3 in
`elapsed_turns`.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed. A test of
the rebuild replays the three turns of each run, and the rebuild makes the same
calls with the same bounds and results and reaches the same generator position
and state.

## Conclusion

Three turns of Greed agree with the spec for both seeds. No computer player
hired in turn 2 or 3, and no vacant offer was drawn in turn 3.
