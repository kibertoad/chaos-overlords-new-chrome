---
id: EXP-TURN-059
title: Does a Dominance match at Criminal played for 34 turns draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-059.json
---

## Question

Over 34 turns of Dominance at Criminal, in which computer players plan Moves
to distant sectors, do the rebuild's computer players plan and resolve as the
original's?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Dominance (`--scenario 3`), Mentality 1
(`--mentality 1`), a turn limit of 104 (`--turns 104`), 34 Done presses
(`--end-turns 34`) and `--seed 3101`, with no orders.

## Observations

The run made 22584 calls of `roll` over 34 Done presses. At the end
`elapsed_turns` is 34, every player is still active, and the scenario scores
of players 0 to 5 are 30, 587, 396, 411, 514 and 528.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off. The rebuild
makes the same calls with the same bounds and results and reaches the same
state, planning records, sector weights and per-player values. With DEV-AI-007
on, it refuses the computer players' Moves to sectors more than one step away,
and the calls part at the first of them.

## Conclusion

The run agrees with RULE-AI-006 and RULE-MOVE-001 over 34 turns, including the
turns in which the sector selector returns a sector several steps away and
the Move pass puts the gang there.
