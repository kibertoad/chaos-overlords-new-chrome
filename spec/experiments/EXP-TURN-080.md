---
id: EXP-TURN-080
title: Does a family-12 computer gang in a hostile human's sector draw from every visible gang when its weight is 1, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-080.json
---

## Question

RULE-AI-030 draws from the human players' gangs only when the sector's weight
is 10 and its owner is a human the player is hostile to. When the owner is
such a human but the first visible gang is a computer player's, so the weight
is 1, does the family-12 gang draw from every visible gang? Does an unopposed
gang with no weapon or armor to buy equip a Detect item?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Armageddon (`--scenario 9`), Mentality 3 (`--mentality
3`), nineteen Done presses (`--end-turns 19`) and `--seed 7`. The human gives
no orders. Before the fourteenth Done press the probe writes 12 into the
`family` of the planning records in every roster slot of the computer players
that held a gang at the time (FMT-STATE-007, `--families
14:1:0:12,14:1:1:12,14:1:2:12,14:1:3:12,14:1:4:12,14:1:5:12,14:1:6:12,14:1:7:12,14:1:8:12,14:1:9:12,14:2:0:12,14:2:1:12,14:2:2:12,14:2:3:12,14:2:4:12,14:2:5:12,14:2:6:12,14:2:7:12,14:2:8:12,14:2:9:12,14:3:0:12,14:3:1:12,14:3:2:12,14:3:3:12,14:3:4:12,14:3:5:12,14:3:6:12,14:3:7:12,14:3:8:12,14:3:9:12,14:4:0:12,14:4:1:12,14:4:2:12,14:4:3:12,14:4:4:12,14:4:5:12,14:4:6:12,14:4:7:12,14:4:8:12,14:4:9:12,14:5:0:12,14:5:1:12,14:5:2:12,14:5:3:12,14:5:4:12,14:5:5:12,14:5:6:12,14:5:7:12,14:5:8:12,14:5:9:12`),
as EXP-TURN-040 does.

The seed and the writes were found in the rebuild, which played every scenario
at every Mentality for seeds 6 to 12 with each family written into every
computer gang at turn 3, 8 or 14, and recorded which planner branches no
recorded run had reached. This run reaches the RULE-AI-030 branches named
under Results.

## Observations

The run made 5446 calls of `roll` over nineteen Done presses. At the end
`elapsed_turns` is 19 and player 0's `controller` is -2: the human was
eliminated during the run.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls with the same bounds and results and
reaches the same state, the planning records included. Before the rebuild was
corrected it diverged at call 3335: the original drew at `0x004354A9` among
three gangs, the draw from every visible gang (FND-RNG-006), where the rebuild
drew among the one human gang because it took the human-only pool without
testing the weight. The corrected rebuild makes the same draw. The run also
reaches the human-only pool at weight 10, a further draw after a failed
strength test, and the Equip of a Detect item.

## Conclusion

The run agrees with RULE-AI-030: the human-only pool needs weight 10, a failed
strength test draws again, and an unopposed family-12 gang equips a Detect
item when it has nothing else to buy.
