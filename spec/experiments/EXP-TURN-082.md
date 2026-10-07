---
id: EXP-TURN-082
title: Does a family-4 computer gang that has moved twice take the sector by Control, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-082.json
---

## Question

EXP-TURN-074 reaches the family-4 test for Control after two Moves, which
fails there every time. When a family-4 gang outside its own sectors, at a
weight below 10, has moved in its last two actions and can take the sector
alone, does it plan Control, as RULE-AI-023 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
five Done presses (`--end-turns 5`) and `--seed 1`. The human gives no orders.
Before the third Done press the probe writes 4 into the `family` of the
planning records in roster slots 0 and 1 of each computer player
(FMT-STATE-007, `--families
3:1:0:4,3:1:1:4,3:2:0:4,3:2:1:4,3:3:0:4,3:3:1:4,3:4:0:4,3:4:1:4,3:5:0:4,3:5:1:4`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 12, with families 0, 4, 5 and 7 written into every
computer gang at turn 3, 8 or 14 and with no write, and recorded which of a
list of unreached planner branches each match took.

## Observations

The run made 696 calls of `roll` over five Done presses. At the end
`elapsed_turns` is 5 and no player's `controller` has changed.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off and writes
the same families before the same Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state, the planning
records included. The rebuild reaches the family-4 Control after two Moves and
the Heal after None, Control or Heal.

## Conclusion

The run agrees with RULE-AI-023 for Control after two Moves.
