---
id: EXP-TURN-078
title: Does a family-6 computer gang that fails its first draw with nothing to buy make the further draws, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-078.json
---

## Question

No other recorded run has a family-6 computer gang fail its first attack draw
in a sector it weighs above 0 with no weapon or armor upgrade to buy. Does it
then fail the Heal test, which needs weight 0, and attack the last of up to
five further draws, as RULE-AI-025 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Big Man (`--scenario 8`), Mentality 3 (`--mentality 3`),
nineteen Done presses (`--end-turns 19`) and `--seed 11`. The human gives no
orders. Before the fourteenth Done press the probe writes 6 into the `family`
of the planning records in every roster slot of the computer players that held
a gang at the time (FMT-STATE-007, `--families
14:1:0:6,14:1:1:6,14:1:2:6,14:1:3:6,14:1:4:6,14:1:5:6,14:1:6:6,14:1:7:6,14:2:1:6,14:2:2:6,14:2:3:6,14:2:4:6,14:2:5:6,14:3:0:6,14:3:1:6,14:3:3:6,14:3:4:6,14:4:0:6,14:4:1:6,14:4:2:6,14:4:3:6,14:4:4:6,14:4:5:6,14:4:6:6,14:4:7:6,14:5:0:6,14:5:1:6,14:5:2:6,14:5:3:6,14:5:4:6,14:5:5:6`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played every scenario
at every Mentality for seeds 6 to 12 with each family written into every
computer gang at turn 3, 8 or 14, and recorded which planner branches no
recorded run had reached. This run reaches the RULE-AI-025 branches named
under Results.

## Observations

The run made 3365 calls of `roll` over nineteen Done presses. At the end
`elapsed_turns` is 19 and every player is still active.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off and writes
the same families before the same Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state, the planning
records included. The rebuild reaches the failed first draw with no upgrade to
buy, the Heal test that fails at a positive weight, and the further draws ending
in an Attack. A family-3 gang of the same run heals, which no other recorded run
reaches either (RULE-AI-022).

## Conclusion

The run agrees with RULE-AI-025 for the further draws after a failed first
draw, and with RULE-AI-022 for the family-3 Heal.
