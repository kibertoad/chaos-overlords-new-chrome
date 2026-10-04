---
id: EXP-TURN-079
title: Does a family-4 computer gang whose previous action was Chaos or Equip draw attacks at weight 10, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-079.json
---

## Question

EXP-TURN-074 reaches the family-4 attack draws after a Move. When the previous
action was Chaos or Equip and the gang stands where its weight is 10, does it
draw up to the given number of targets and attack the last one drawn, as
RULE-AI-023 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Siege (`--scenario 6`), Mentality 2 (`--mentality 2`),
fifteen Done presses (`--end-turns 15`) and `--seed 11`. The human gives no
orders. Before the fourteenth Done press the probe writes 4 into the `family`
of the planning records in every roster slot of the computer players that held
a gang at the time (FMT-STATE-007, `--families
14:1:0:4,14:1:1:4,14:1:2:4,14:1:3:4,14:1:4:4,14:1:5:4,14:1:6:4,14:1:7:4,14:1:8:4,14:1:9:4,14:2:0:4,14:2:1:4,14:2:2:4,14:2:3:4,14:2:4:4,14:2:5:4,14:2:6:4,14:2:7:4,14:2:8:4,14:2:9:4,14:3:0:4,14:3:1:4,14:3:2:4,14:3:3:4,14:3:4:4,14:3:5:4,14:3:6:4,14:3:7:4,14:3:8:4,14:3:9:4,14:4:0:4,14:4:1:4,14:4:2:4,14:4:3:4,14:4:4:4,14:4:5:4,14:4:6:4,14:4:7:4,14:4:8:4,14:4:9:4,14:5:0:4,14:5:1:4,14:5:2:4,14:5:3:4,14:5:4:4,14:5:5:4,14:5:6:4,14:5:7:4,14:5:8:4,14:5:9:4`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played every scenario
at every Mentality for seeds 6 to 12 with each family written into every
computer gang at turn 3, 8 or 14, and recorded which planner branches no
recorded run had reached. This run reaches the RULE-AI-023 branches named
under Results.

## Observations

The run made 4837 calls of `roll` over fifteen Done presses. At the end
`elapsed_turns` is 15 and player 0's `controller` is -2: the human was
eliminated during the run.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. The rebuild reaches the
attack draws that follow a Chaos or an Equip at weight 10 and plans the Attack
on the last target drawn.

## Conclusion

The run agrees with RULE-AI-023 for the attack draws after Chaos or Equip.
