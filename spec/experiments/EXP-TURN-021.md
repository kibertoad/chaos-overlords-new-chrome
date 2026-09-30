---
id: EXP-TURN-021
title: Does a computer gang in an enemy sector count every other player's visible gang there before it plans Control?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-021.json
---

## Question

Over twenty-nine turns, with the human's gang walking through computer-owned
land, do the computer players plan and resolve as the spec gives? The run was
made to reach a family-3 attack and single out RULE-AI-004's
`solo_control_ok` instead.

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-004 with the seed of EXP-TURN-013 (`--seed 46976`), pressing
   Done twenty-nine times (`--end-turns 29`), with a Move (10) of the
   human's gang in roster slot 0 and 0 in `repeat_action` before twenty of
   the presses:

   ```
   --orders 2:0:10:22:0:0,3:0:10:21:0:0,4:0:10:29:0:0,5:0:10:22:0:0,8:0:10:21:0:0,9:0:10:12:0:0,10:0:10:21:0:0,12:0:10:20:0:0,14:0:10:21:0:0,15:0:10:20:0:0,16:0:10:13:0:0,18:0:10:14:0:0,19:0:10:7:0:0,21:0:10:14:0:0,22:0:10:5:0:0,23:0:10:14:0:0,24:0:10:21:0:0,25:0:10:22:0:0,26:0:10:29:0:0,27:0:10:37:0:0
   ```

2. Repeat step 1, noting every call of the sector selector `0x00408642`
   (`--trace-calls 0x00408642`) and copying the writable sections at the
   entry of call 16671 of `roll`, counting from 0 (`--dump-at-roll 16671`).

## Observations

The seed was 46976 in both runs. Each made 18909 calls of `roll`, 315 before
the first Done press, with the same bounds and results. No call was made at
the attack draws of families 3 and 5 (FND-RNG-006). In the copy after the last
turn the human's gang was alive in sector 37.

In the traced run, in the planning of turn 28, player 3's selector calls were
for gangs 6, 8, 11 and 15, in that order, and the call for gang 8 made call
16671, `roll(3)` at `0x00409C24`, and returned 13. In the copy at the entry
of that call, gang 8 of player 3, with Force 10, stood in sector 6; its call
came from `0x00435189` in family 1's handler `fn_00434080`. Sector 6 was player 1's; one gang of player 1, with Force 5, and
two of player 2, with Force 8 and 9, stood in it too.

## Results

Every result is the one RULE-RNG-002 computes from the recorded seed.
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the first run.
The rebuild makes the same calls with the same bounds and results and reaches
the same generator position and state after the twenty-nine turns.

A reading of `solo_control_ok` in which the defence counted the owner's gangs
alone, and the Support of the sites the owner influenced, first differed at
call 16671: gang 8, whose previous action was Move, planned Control of sector
6 with no call of the selector, so the next selector call, for gang 15, drew
`roll(8)` there. RULE-AI-004's reading, which adds the sector's `support`
and every visible gang of every other player in an owned sector, refuses the
Control, and gang 8 calls the selector as the original does.

## Conclusion

The run agrees with RULE-AI-004: an estimate of a lone Control counts every
other player's visible gang in the sector as a defender, whoever owns it. The
route reached no family-3 attack in the original.
