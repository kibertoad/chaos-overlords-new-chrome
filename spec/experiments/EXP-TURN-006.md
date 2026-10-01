---
id: EXP-TURN-006
title: Do three turns of new local Armageddon games, each ended with no orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-006.json
---

## Question

As EXP-TURN-001, over three turns of scenario 9: do the computer players'
planning and the resolutions make the draws and reach the state the spec
gives?

## Setup

As EXP-TURN-005, with 9 written into the scenario dword and its preference
byte.

## Procedure

1. As EXP-TURN-002, pressing Done three times (`--scenario 9 --end-turns 3`).
2. Start the first run again with its recorded seed (`--seed 51242`) and copy
   the writable sections at the entry of call 518 of `roll`, counting from 0
   (`--dump-at-roll 518`).

## Observations

The seeds were 51242 and 21674. Begin made 323 and 352 calls of `roll`. The
three turns made 99, 89 and 107 more in the first run, and 102, 92 and 118 in
the second:

| Calls by turn, first run | Calls by turn, second run | Call instruction | Bound |
|---|---|---|---|
| 15, 5, 5 | 15, 5, 5 | `0x0047172A` | 89 |
| 0, 0, 3 | 0, 0, 2 | `0x00409C24` | 8 |
| 0, 0, 1 | 0, 0, 1 | `0x00409C99` | 258 |
| 5, 5, 5 | 5, 5, 5 | `0x00475AC6` | 5 |
| 79, 79, 93 | 82, 82, 105 | `0x00475FBB` | 6 |

The copies held 9 in `scenario` and 3 in `elapsed_turns`.

At the entry of call 518 of the repeated first run, the `roll(258)`,
every pair of `selector_pairs` and every entry of the selector's score table
held 0, and so did all 0x510 bytes of player 0's planning records from
`0x0048A250`. The first four bytes of player 1's first record, at
`0x0048A760`, were 0, 0, 0 and 3.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the three
turns of each run, and the rebuild makes the same calls with the same bounds
and results and reaches the same generator position and state.

## Conclusion

The three turns agree with RULE-AI-006 as FND-AI-066 gives it. In turn 3 of
both runs a gang's selector call scored no sector. Its tie count ran over the
64 pairs, the 32 pairs of the score table and the 162 pairs of the human
player's records, which stay zero bytes because that player never has a
planning pass, and stopped at the first record of player 1: `roll(258)`.
