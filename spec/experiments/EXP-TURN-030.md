---
id: EXP-TURN-030
title: Do two Gives swap weapons, and does the later of two Gives to one gang replace both the earlier one and a weapon bought that turn?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-030.json
---

## Question

RULE-GIVE-001 gives three edge cases no run had recorded: two gangs can swap
items by giving to each other in the same turn; a later Give to the same
recipient overwrites the pending item, and the earlier giver's item is lost;
and the delivered item replaces what the recipient holds in that slot,
including an item it bought this turn. Does the original do all three?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 4` and four Done presses (`--end-turns 4`).
The human's gang starts in sector 33.

1. Turn 1: roster slot 0 equips item 0, a weapon of Cost 1, and the probe
   writes offer slot 1's hire order with sector 33.
2. Turn 2: the hired gang in slot 1 equips item 1, a weapon of Cost 1, and
   offer slot 1 is hired into sector 33 again.
3. Turn 3: slot 0 gives its weapon (mask 1) to slot 1, and slot 1 gives its
   weapon to slot 0.
4. Turn 4: slots 0 and 1 both give their weapons to slot 2, and slot 2 equips
   item 12, a weapon of Cost 3.

(`--orders 1:0:5:0:0:0,2:1:5:1:0:0,3:0:6:1:1:0,3:1:6:1:0:0,4:0:6:1:2:0,4:1:6:1:2:0,4:2:5:12:0:0 --hires 1:1:33,2:1:33`)

The seed was found in the rebuild, which played this plan for seeds 1 to 300.
Seed 4 affords every order, and no computer player is given a Move that
DEV-AI-007 refuses.

## Observations

The run made 785 calls of `roll`. At the end the human had 4 cash, the gangs
in slots 0 and 1 held no weapon, and the gang in slot 2 held item 0.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state.

Slot 2 ends with item 0. Only the swap in turn 3 puts item 0 in slot 1, whose
Give in turn 4 is the later of the two, so the turn 3 Gives swapped the
weapons. Item 1, which slot 0 gave in turn 4, and item 12, which slot 2 bought
in the same turn, are both gone.

## Conclusion

The run agrees with RULE-GIVE-001: the two Gives swap the weapons, the later
Give to one recipient replaces the earlier one, and the delivered weapon
replaces the one the recipient bought that turn.
