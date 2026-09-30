---
id: EXP-TURN-011
title: Does a human gang's Attack on a computer player's gang in its sector draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-011.json
---

## Question

In the first run of EXP-TURN-010, a gang of player 4 stands in sector 12 with
the human's hiding gang from turn 23 on. When the human's gang attacks it
instead of hiding, do the attack, the retaliation, the damage and the
attitude change draw and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-010's first run, with its seed, pressing Done twenty-four times
   (`--seed 61038 --end-turns 24`), with two orders: before the first Done
   press, Hide (8) in `action` and `repeat_action` of the human's gang in
   roster slot 0; before the twenty-fourth, Attack (1) in its `action`, player
   4 in `target`, roster slot 18 in `target_2` and 0 in `repeat_action` and
   `repeat_target` (`--orders 1:0:8:0:0:1,24:0:1:4:18:0`).
2. Repeat the run and copy the writable sections at the entry of call 11600 of
   `roll`, counting from 0, the second call after the twenty-fourth Done press
   (`--dump-at-roll 11600`).

## Observations

Begin made 307 calls of `roll` and the twenty-four turns made 12316 more. The
twenty-fourth Done press came after call 11598, and the turn it ended made
1024 calls, 1000 of them at `0x00475FBB`. The copy at call 11600 showed the human's gang in sector
12 with Force 10, `action` 1, `target` 4 and `target_2` 18, and gang 18 of
player 4 in sector 12 with Force 9, a type 0 weapon and `action` 0.

At the end of the run the human's gang had Force 6, `action` and
`repeat_action` 0 and `target_2` 18, and player 4's gang 18 still had Force 9.
Player 4's `attitude` toward player 0 was -10 and player 0's toward player 4
was 10.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run,
giving the Attack as a command for player 4's roster slot 18. The rebuild makes
the same calls with the same bounds and results and reaches the same generator
position and state. In it the human's gang, Force 10 and Combat 1 against
Defense 9, rolls two dice and hits with none, so it does no damage; the target,
Force 9 and Combat 7 against Defense 1, strikes back with fifteen dice, nine of
them hits, for four damage, which leaves the human's gang at Force 6 when the
combat phase ends.

## Conclusion

The run agrees with RULE-ATTACK-001 and RULE-COMBAT-002 for an attack on a gang
that is not hiding: the opening pool, the success threshold, the retaliation
at half the target's hits, and damage taken off Force only at the end of the
phase. The run does not test Damage Inflicted (RULE-COMBAT-003), which the
fixture does not record and which this attack left unchanged. It tests the
attitude change (RULE-AI-016) only in part: player 4's attitude toward the
human, -10 before the turn, rises to -9 at the start of the resolution
(RULE-AI-015) and ends at -10, where EXP-TURN-010's run without the Attack
goes on to -8 a turn later, so the Attack lowered it by at least one, and the
floor of -10 hides by how much.
