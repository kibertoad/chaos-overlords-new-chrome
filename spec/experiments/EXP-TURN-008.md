---
id: EXP-TURN-008
title: Do ten turns of new local games with Mentality 3, each ended with no orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-008.json
---

## Question

As EXP-TURN-004, over ten turns with `mentality` 3: do the computer players'
planning, hiring and sector choices at the highest Mentality make the draws and
reach the state the spec gives?

## Setup

As EXP-TURN-001. Before pressing Begin the probe wrote 3 into `mentality` at
`0x00487850` (FND-SETUP-013). The scenario and `turn_limit` were left as the
setup screen set them.

## Procedure

As EXP-TURN-002, pressing Done ten times (`--mentality 3 --end-turns 10`).

## Observations

The seeds were 39407 and 913. Begin made 305 and 312 calls of `roll`. The ten
turns made 99, 110, 116, 97, 169, 250, 194, 300, 340 and 361 more in the first
run, and 97, 94, 100, 174, 183, 246, 263, 367, 354 and 452 in the second. Over
the ten turns the calls were:

| Call instruction | Bound | First run | Second run |
|---|---|---|---|
| `0x0047172A` | 89 | 60 | 61 |
| `0x00409C24` | 3 to 8 | 27 | 29 |
| `0x00409C99` | 4 and 8 | 0 | 2 |
| `0x00475AC6` | 5 | 38 | 44 |
| `0x00475FBB` | 6 | 1911 | 2194 |

The copies held 4 in `scenario`, 3 in `mentality` and 10 in `elapsed_turns`.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed. A test of
the rebuild replays the ten turns of each run, and the rebuild makes the same
calls with the same bounds and results and reaches the same generator position
and state.

## Conclusion

Ten turns at Mentality 3 agree with the spec for both seeds.
