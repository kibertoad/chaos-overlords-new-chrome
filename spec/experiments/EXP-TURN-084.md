---
id: EXP-TURN-084
title: Does a family-14 computer gang on a contested objective draw even when the pool is empty, as the static reading gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-084.json
---

## Question

RULE-AI-031 gave the family-13 and family-14 draw loop a test that the pool is
not empty. FND-AI-062 reads no such test: the loop runs while its counter is
below 5 and its stop flag is clear, and a negative draw sets the flag. When
the pool of the sector owner's visible gangs is empty, does the original still
draw?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Big Man (`--scenario 8`), Mentality 3 (`--mentality 3`),
twenty-one Done presses (`--end-turns 21`) and `--seed 6`. The human gives no
orders. Before the fourteenth Done press the probe writes 5 into the `family`
of the planning records in every roster slot of the computer players that held
a gang at the time (FMT-STATE-007, `--families
14:1:0:5,14:1:1:5,14:1:2:5,14:1:3:5,14:1:4:5,14:1:5:5,14:1:6:5,14:2:1:5,14:2:3:5,14:3:0:5,14:3:1:5,14:3:2:5,14:3:3:5,14:3:4:5,14:3:5:5,14:3:6:5,14:3:7:5,14:4:0:5,14:4:1:5,14:4:2:5,14:4:3:5,14:4:4:5,14:4:5:5,14:4:6:5,14:4:7:5,14:5:0:5,14:5:1:5,14:5:2:5,14:5:3:5,14:5:4:5,14:5:5:5`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 12, with families 0, 4, 5 and 7 written into every
computer gang at turn 3, 8 or 14 and with no write, and recorded which of a
list of unreached planner branches each match took.

## Observations

The run made 4033 calls of `roll` over twenty-one Done presses. At the end
`elapsed_turns` is 21 and no player's `controller` has changed. After the
sixth Done press the original calls `roll(0)` at `0x00466A7A`, the family-14
draw from the sector owner's visible gangs (FND-RNG-006), and its next call is
a hire-offer draw at `0x0047172A`: the loop makes no second draw. The family
writes were chosen for family-5 branches that the rebuild reached before it
was corrected, and that the corrected rebuild does not reach in this match.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. Before the rebuild was
corrected it made no draw there, as RULE-AI-031 then gave, and diverged at
call 805. The corrected rebuild draws once with a bound of 0, which rolls as 1
(RULE-RNG-002), and the lookup in the empty list gives no gang, which ends the
loop.

## Conclusion

The run agrees with FND-AI-062: an empty pool still makes one draw, `roll(0)`,
and its lookup gives no gang. RULE-AI-031 is corrected to match.
