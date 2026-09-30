---
id: EXP-TURN-010
title: Do twenty-five turns of new local games, with the human's first gang hiding throughout, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-010.json
---

## Question

As EXP-TURN-004, over twenty-five turns, with the human's first gang on a
recurring Hide so that the human stays in the game: do the later turns, with
many more gangs, Crackdowns and police, make the draws and reach the state the
spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-009, pressing Done twenty-five times (`--end-turns 25`), with
   one order: before the first Done press, Hide (8) in `action` and
   `repeat_action` of the human's gang in roster slot 0 (`--orders 1:0:8:0:0:1`).
2. Start the first run again with its recorded seed, twenty-four turns and
   the same order (`--seed 61038 --end-turns 24 --orders 1:0:8:0:0:1`), note
   every call of the sector selector `0x00408642` (`--trace-calls 0x00408642`)
   and copy the writable sections at the entry of call 11610 of `roll`,
   counting from 0 (`--dump-at-roll 11610`).

## Observations

The seeds were 61038 and 13847. Begin made 307 and 320 calls of `roll`, and
the twenty-five turns made 13260 and 13258 more. Over the twenty-five turns the
calls were:

| Call instruction | Bound | First run | Second run |
|---|---|---|---|
| `0x0047172A` | 89 | 137 | 136 |
| `0x00409C24` | 2 to 8 | 145 | 163 |
| `0x00409C99` | 2 to 9 in the first run; 4, 7, 8 and 258 in the second | 14 | 5 |
| `0x004737A9` | 3 | 1 | 0 |
| `0x0047419B` | 100 | 2 | 0 |
| `0x004756D9` | 2 | 0 | 1 |
| `0x00475AC6` | 5 | 110 | 115 |
| `0x00475FBB` | 6 | 12851 | 12838 |

In the first run the call at `0x004737A9` is call 11572 and the calls at
`0x0047419B` are calls 11573 and 12580, in turns 23 and 24. The second run's
call at `0x004756D9` is call 7217, in turn 18. The Last Turn Events panel
opened once, at the planning phase after the first run's last turn. The
copies held 25 in `elapsed_turns`, and the human's gang had Force 10 and
`action` and `repeat_action` 8 in both runs.

In the traced repetition of the first run, player 4's selector calls after
call 11609 of `roll` were, in order, for gangs 3, 8, 11 and 15, with mode 5
each. They returned 22, 11, 62 and 45; the call for gang 8 was entered after
11609 calls and the call for gang 11 also after 11609, so gang 8's call made
no call of `roll` and gang 11's made call 11609. At the entry of call 11610,
the `roll(7)` at `0x00409C99` of gang 15's call, the first seven pairs of
`selector_pairs` held score 1 with the sectors 0, 45, 46, 53, 55, 61 and 62.
Planning records 8 and 11 of player 4 held family 1, and 3 and 15 family 0.
Sector 12 was owned by player 0, the human, and a gang of player 4 stood in
it; player 4's `attitude` toward player 0 was -10.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays both runs. The
rebuild makes the second run's calls with the same bounds and results and
reaches the same generator position and state. In the first run it makes
calls 0 to 11609 with the same bounds and results, including the Crackdown and
police calls, and differs from call 11610 on: its call for player 4's gang 8,
in sector 20, scores sectors 11 and 12 at 5 and calls `roll(2)`, which is call
11609 and happens to match the original's call for gang 11.

## Conclusion

The second run agrees with the spec over twenty-five turns, including a tied
Control. The first run disagrees with RULE-AI-006 as the rebuild implements
it: the original did not give sector 12 the score of 5 that mode 5 and the
multiply by five give a sector held by a human the player is hostile to, or
its late filter for family 1 removed the sector. Which one is not settled. The
test keeps the first run as a known divergence at call 11610.
