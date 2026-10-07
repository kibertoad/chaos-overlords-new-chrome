---
id: EXP-TURN-062
title: Does a Greed match at Goon with a standing Chaos order play 26 turns as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-062.json
---

## Question

When the human's gang holds a standing Chaos order from turn 1, do the
computer players of a Greed match at Goon plan and resolve over 26
turns as the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0
(`--mentality 0`), 26 Done presses (`--end-turns 26`) and
`--seed 1002`. Before the Done press of turn 1, write Chaos (3) into the
`action` and `repeat_action` bytes of the human's gang in roster slot 0, with
0 in `target` and `target_2` (`--orders 1:0:3:0:0:1`), so the gang repeats
Chaos every turn (FMT-STATE-001).

## Observations

The run made 9570 calls of `roll` over 26 Done presses. At the end
`elapsed_turns` is 26, every player is still active, and the scenario
scores of players 0 to 5 are 163, 353, 182, 566, 238 and 328.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off. The rebuild
makes the same calls with the same bounds and results and reaches the same
state, planning records, sector weights and per-player values.

## Conclusion

The run agrees with the spec's computer planning and hiring, the human's
repeated Chaos order and the police over 26 turns of Greed.
