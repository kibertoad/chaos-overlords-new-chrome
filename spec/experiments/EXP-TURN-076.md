---
id: EXP-TURN-076
title: Does a family-3 computer gang keep an Attack whose drawn target passes the strength test, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-076.json
---

## Question

EXP-TURN-022 reaches the draw on a visible human gang. Does a family-3 gang
whose drawn target passes the strength test plan the Attack on it, as
RULE-AI-022 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 3 (`--mentality 3`),
seven Done presses (`--end-turns 7`) and `--seed 2`. The human gives no
orders. Before the third Done press the probe writes 3 into the `family` of
the planning records in roster slots 0 and 1 of each computer player
(FMT-STATE-007, `--families
3:1:0:3,3:1:1:3,3:2:0:3,3:2:1:3,3:3:0:3,3:3:1:3,3:4:0:3,3:4:1:3,3:5:0:3,3:5:1:3`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played Greed, Power
and Kill 'Em All at every Mentality for seeds 1 to 5 with each family written
at turn 3 or 7, and recorded which planner branches no recorded run had
reached. This run reaches the RULE-AI-022 branches named under Results.

## Observations

The run made 951 calls of `roll` over seven Done presses. At the end
`elapsed_turns` is 7 and every player is still active.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. The rebuild reaches the
family-3 attack draw with an accepted target and plans the Attack.

## Conclusion

The run agrees with RULE-AI-022 for an accepted family-3 attack draw.
