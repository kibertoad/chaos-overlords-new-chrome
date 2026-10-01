---
id: EXP-TURN-009
title: Do twelve turns of new local games, with the human's first gang given Snitch, recurring Chaos, Hide and Bribe orders, draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-009.json
---

## Question

As EXP-TURN-004, over twelve turns in which the human's first gang carries out
orders: do Snitch, Bribe, Hide and a recurring Chaos, and a Crackdown they
bring on, make the draws and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-002, pressing Done twelve times (`--end-turns 12`).
2. Before the Done presses of turns 1, 2 and 3, write Snitch (13) into the
   `action` byte of the human's gang in roster slot 0, with 0 in `target`,
   `target_2`, `repeat_action` and `repeat_target` (FMT-STATE-001). Before
   turn 4 write Chaos (3) into `action` and `repeat_action`. Before turn 8
   write Hide (8) with 0 in `repeat_action`, before turn 9 Bribe (2) and
   before turn 11 Chaos, each with 0 in the other four bytes
   (`--orders 1:0:13:0:0:0,2:0:13:0:0:0,3:0:13:0:0:0,4:0:3:0:0:1,8:0:8:0:0:0,9:0:2:0:0:0,11:0:3:0:0:0`).
   These are the bytes RULE-TURN-005 has the order screens write.
3. At the start of each planning phase, press Exit on the Combat Results
   panel and the Last Turn Events panel while either is open (SCR-COMBAT-001,
   SCR-EVENT-001).

## Observations

The seeds were 15290 and 27776. Begin made 333 and 320 calls of `roll`. The
twelve turns made 100, 103, 115, 170, 219, 316, 316, 347, 419, 406, 503 and
557 more in the first run, and 100, 97, 101, 163, 208, 264, 297, 324, 384,
354, 472 and 556 in the second. Over the twelve turns the calls were:

| Call instruction | Bound | First run | Second run |
|---|---|---|---|
| `0x0047172A` | 89 | 70 | 73 |
| `0x00409C24` | 2 to 8 | 38 | 35 |
| `0x00409C99` | 4 | 1 | 0 |
| `0x00475AC6` | 5 | 56 | 54 |
| `0x00475FBB` | 6 | 3406 | 3158 |

The human's gang stood in sector 9 in the first run and in sector 33 in the
second. In the first run the planning phase of turn 5 opened the Last Turn
Events panel with one report, a Crackdown in sector 9 dated the fourth
turn. The probe of this recording had no breakpoint on that panel, so Exit was
pressed by hand, and the probe's repeated Done presses while the panel was open
drew no call of `roll`. In the second run no report was shown.

The copies held 12 in `elapsed_turns`, and the human's gang had Force 10 in
both runs.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the twelve
turns of each run, submitting each written order as the human's command before
the Done press it preceded, and the rebuild makes the same calls with the same
bounds and results and reaches the same generator position and state.

## Conclusion

The human's Snitch, Bribe, Hide and recurring Chaos agree with RULE-SNITCH-001,
RULE-BRIBE-001, RULE-HIDE-001, RULE-CHAOS-001 and RULE-TURN-004 for both
seeds, and so does the Crackdown the first run's Snitches and Chaos brought on
in the human's sector.
