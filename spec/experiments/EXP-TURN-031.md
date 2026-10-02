---
id: EXP-TURN-031
title: Does an Equip succeed at exactly its price, fail one short, and count a Sell only from an earlier roster slot?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-031.json
---

## Question

RULE-EQUIP-001 pays an Equip from the cash the player has when the
transaction pass reaches the gang, and RULE-EQUIP-002 scans the player's gangs
by roster slot. So an Equip at exactly the player's cash succeeds, one short
fails and leaves the cash, a Sell by an earlier roster slot can pay the
shortfall, and a Sell by a later slot cannot. Does the original do all four?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 1` and nine Done presses (`--end-turns 9`).
The human's gang starts in sector 51 with 20 cash.

1. Turn 1: roster slot 0 equips item 24, an armor of Cost 2, and the probe
   writes offer slot 0's hire order with sector 51; the gang it hires costs 3.
2. Turn 2: the hired gang in slot 1 equips item 24.
3. Turns 3 to 5: slot 0 bribes, for 3 each time.
4. Turn 6, with 4 cash: slot 1 equips item 40, a miscellaneous item of Cost 4.
5. Turn 7, with 0 cash: slot 1 equips item 0, a weapon of Cost 1.
6. Turn 8, with 0 cash: slot 0 equips item 0 and slot 1 sells its armor
   (mask 2), which pays 1.
7. Turn 9, with 1 cash: slot 0 sells its armor and slot 1 equips item 24.

(`--orders 1:0:5:24:0:0,2:1:5:24:0:0,3:0:2:0:0:0,4:0:2:0:0:0,5:0:2:0:0:0,6:1:5:40:0:0,7:1:5:0:0:0,8:0:5:0:0:0,8:1:12:2:0:0,9:0:12:2:0:0,9:1:5:24:0:0 --hires 1:0:51`)

Each turn's orders are given in roster order, so the order the rebuild
resolves Equip and Sell in (DEV-EQUIP-001) is the original's scan order.

The seed was found in the rebuild, which played this plan for seeds 1 to 300,
bribing down to a few cash and then placing each case at the cash of that
turn. In seed 1 the player's income and upkeep cancel from turn 6, and no
computer player is given a Move that DEV-AI-007 refuses.

## Observations

The run made 1908 calls of `roll`. At the end the human had 0 cash, the gang
in slot 0 held no item, and the gang in slot 1 held armor 24 and
miscellaneous item 40.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state. In its replay the cash at each planning
turn from 6 to 10 is 4, 0, 0, 1 and 0: the Equip of turn 6 spends the 4, the
Equips of turns 7 and 8 fail for lack of cash, the Sell of turn 8 pays 1, and
in turn 9 the Sell raises the 1 to 2, which the Equip spends.

Slot 1 ends with item 40 and item 24 and slot 0 with no weapon, which only
this outcome gives: the Equip at the exact price succeeded, both Equips one
short failed, the Sell by the later slot did not pay for the Equip of turn 8,
and the Sell by the earlier slot paid for the Equip of turn 9.

## Conclusion

The run agrees with RULE-EQUIP-001 and RULE-EQUIP-002.
