---
id: EXP-TURN-060
title: Does a Siege match at Criminal played for 23 turns draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-060.json
---

## Question

Over 23 turns of Siege at Criminal, in which computer players plan Moves to
distant sectors, do the rebuild's computer players plan and resolve as the
original's?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Siege (`--scenario 6`), Mentality 1 (`--mentality 1`),
23 Done presses (`--end-turns 23`) and `--seed 6101`, with no orders.

## Observations

The run made 8099 calls of `roll` over 23 Done presses. At the end
`elapsed_turns` is 23, every player is still active, and the scenario scores
of players 0 to 5 are 0, 1, 1, 1, 2 and 1.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, planning records, sector weights and
per-player values. With DEV-AI-007 on, it refuses the computer players' Moves
to sectors more than one step away, and the calls part at the first of them.

## Conclusion

The run agrees with RULE-AI-006 and RULE-MOVE-001 over 23 turns of Siege,
including the turns in which the sector selector returns a sector several
steps away and the Move pass puts the gang there.
