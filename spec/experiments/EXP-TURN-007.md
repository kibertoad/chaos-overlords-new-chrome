---
id: EXP-TURN-007
title: Do fifteen turns of new local games, each ended with no orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-007.json
---

## Question

As EXP-TURN-004, over fifteen turns: once each computer player runs dozens of
gangs, do their planning passes and sector choices, and the resolutions, still
make the draws and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-002, pressing Done fifteen times (`--end-turns 15`).
2. Start the first run again with its recorded seed and ten turns
   (`--seed 35379 --end-turns 10`), and note every call of the sector selector
   `0x00408642` with its arguments and result (`--trace-calls 0x00408642`).
3. Start it again the same way and copy the writable sections at the entry of
   call 1915 of `roll`, counting from 0 (`--dump-at-roll 1915`).

## Observations

The seeds were 35379 and 37432. Begin made 318 and 311 calls of `roll`. The
fifteen turns made 98, 98, 111, 140, 126, 193, 246, 262, 321, 382, 395, 556,
486, 534 and 611 more in the first run, and 101, 98, 105, 151, 195, 252, 280,
352, 385, 373, 470, 509, 548, 525 and 582 in the second. Over the fifteen
turns the calls were:

| Call instruction | Bound | First run | Second run |
|---|---|---|---|
| `0x0047172A` | 89 | 87 | 85 |
| `0x00409C24` | 2 to 8 | 47 | 47 |
| `0x00409C99` | 2 to 10 | 2 | 7 |
| `0x00475AC6` | 5 | 62 | 70 |
| `0x00475FBB` | 6 | 4361 | 4717 |

The copies held 15 in `elapsed_turns`.

In the traced repetition, after the tenth press of Done, the two selector calls
made just before call 1915 were one for player 1's gang 5 with mode `0x42`,
which returned 9, and one for player 2's gang 7 with mode 5, which returned 33
with no call of `roll` between it and the next selector call. At the entry of
call 1915, pair 0 of `selector_pairs` held score 6 with sector 0 and pair 1
held score 1 with sector 41; every other pair and every entry of the score
table but sector 41's, which held 1, held 0.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed. A test of
the rebuild replays the fifteen turns of each run, and the rebuild makes the
same calls with the same bounds and results and reaches the same generator
position and state.

## Conclusion

The fifteen turns agree with RULE-AI-006 as FND-AI-066 and FND-AI-069 give it.
The call with mode `0x42` from sector 16 scored sector 2 once for each of the
six sectors of the board within one step of the gang and sorted that 6 into
pair 0. Player 2's call scored only sector 41 and filtered sector 0, so pair 0
kept the 6 and headed the list alone; the gang in sector 42 stepped toward
sector 0, to 33, with no draw. A selector that scored an encoded sector 1 would
have tied pair 0 with sector 41 and called `roll(2)` at `0x00409C99`.
