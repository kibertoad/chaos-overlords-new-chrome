---
id: EXP-TURN-003
title: Do two turns of new local games, each ended with no orders, hire as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 3
fixture: EXP-TURN-003.json
---

## Question

As EXP-TURN-001, over two turns and three more seeds: do the computer players
choose the same offers to hire and snub as RULE-AI-008 and RULE-AI-009 give?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-002, pressing Done twice (`--end-turns 2`).

## Observations

The seeds were 25081, 53066 and 15514. Begin made 312, 314 and 316 calls of
`roll`; the first turn made 106, 97 and 102 more, and the second 106, 102 and
109:

| Calls in turn 1 | Calls in turn 2 | Call instruction | Bound |
|---|---|---|---|
| 15, 15, 15 | 5, 5, 5 | `0x0047172A` | 89 |
| 0, 0, 0 | 3, 2, 1 | `0x00409C24` | 8 |
| 5, 5, 5 | 4, 5, 5 | `0x00475AC6` | 5 |
| 86, 77, 82 | 94, 90, 98 | `0x00475FBB` | 6 |

The copies held 2 in `elapsed_turns`.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed. A test of
the rebuild replays the two turns of each run, and the rebuild makes the same
calls with the same bounds and results and reaches the same generator position
and state.

## Conclusion

In the run with seed 25081 a computer player with a mode 0 hire role hired,
in turn 1, an offer whose Chaos is at least 0 and whose Control is below 0.
A ranking that tested Control would refuse that offer and lead to a snub
instead; the ranking that tests Chaos, as FND-AI-064 reads it,
hires it. The three runs agree with RULE-AI-008 as FND-AI-064 gives it.
