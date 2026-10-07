---
id: EXP-TURN-004
title: Do six turns of new local games, each ended with no orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-004.json
---

## Question

As EXP-TURN-001, over six turns: once the computer players' gangs have spread
over the map, do their sector choices, their hires and the resolutions still
make the draws and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-002, pressing Done six times (`--end-turns 6`).
2. Start the second run again with its recorded seed written over the argument
   of `srand` at `0x00478CC0` (`--seed 50157`), and at the entry of call 516
   of `roll`, counting from 0, copy the writable sections and the stack
   (`--dump-at-roll 516`).

## Observations

The seeds were 55490 and 50157. Begin made 310 and 314 calls of `roll`. The
six turns made 102, 110, 114, 111, 170 and 261 more in the first run, and 98,
98, 98, 131, 197 and 267 in the second:

| Calls by turn, first run | Calls by turn, second run | Call instruction | Bound |
|---|---|---|---|
| 15, 5, 5, 5, 5, 5 | 15, 5, 5, 5, 5, 5 | `0x0047172A` | 89 |
| 0, 0, 4, 6, 1, 0 | 0, 2, 4, 1, 0, 0 | `0x00409C24` | 8 |
| 0, 0, 0, 0, 0, 2 | 0, 0, 0, 3, 1, 1 | `0x00409C24` | 7 |
| 0, 0, 0, 0, 0, 1 | 0, 0, 0, 0, 2, 2 | `0x00409C24` | 6 |
| 0, 0, 0, 0, 0, 0 | 0, 0, 1, 0, 0, 0 | `0x00409C99` | 9 |
| 4, 4, 5, 2, 4, 4 | 4, 4, 5, 4, 5, 4 | `0x00475AC6` | 5 |
| 83, 101, 100, 98, 160, 249 | 79, 87, 83, 118, 184, 255 | `0x00475FBB` | 6 |

The copies held 6 in `elapsed_turns`.

The repeated second run made calls 0 to 515 with the same results. At the
entry of call 516, the `roll(9)` at `0x00409C99` in turn 3, the first
nine pairs of `selector_pairs` held score 5 with the sectors 6, 24, 25, 26, 32,
34, 40, 41 and 42, and every other pair held 0. The selector's score table
held 5 for the eight sectors 24, 25, 26, 32, 34, 40, 41 and 42 around sector
33, and 0 for sector 6.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed. A test of
the rebuild replays the six turns of each run, and the rebuild makes the same
calls with the same bounds and results and reaches the same generator position
and state.

## Conclusion

The six turns agree with RULE-AI-006 as FND-AI-066 gives it. In turn 3 of the
second run a gang in sector 33 scored the eight sectors around it, but the pair
of sector 6 still held a score of 5 from an earlier call, and this call's late
filter had cleared sector 6 without refilling its pair. That pair headed the
sorted list, so the first pair was not next to the gang, the selector took its
routing branch, and the tie count took in nine pairs. A selector that drew
only among the sectors this call scored would call `roll(8)` at `0x00409C24`
there.
