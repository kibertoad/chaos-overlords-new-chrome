---
id: EXP-TURN-112
title: Does a Siege match at Goon run as the rebuild runs it until the hiding human is eliminated?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-112.json
---

## Question

No recorded run plays Siege beyond turn 82, and none ends a Siege match. With
one human in the match, a recorded run stops when that human is eliminated
(RULE-OBJECTIVE-005). Does a Siege match at Goon, with the human hiding, make
the same draws as the rebuild until the human's last gang dies, and does any
computer player reach the Siege objective first?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-107 with Siege (`--scenario 6`), `--seed 13` and up to 100 Done
presses (`--end-turns 100`). The human's gang in roster slot 0 hides in every
turn (`--orders 1:0:8:0:0:1`). The seed was chosen by playing the same
settings in the rebuild, where the human is eliminated after 97 turns and no
computer player ends the match before that.

## Observations

The run made 52052 calls of `roll` over 96 Done presses. At the end
`elapsed_turns` is 96, the human's `player_active` is 0 and every computer
player is still active. No computer player reached the Siege objective: the
stored scores of players 1 to 5 are 1, 1, 1, 1 and 2.

In the resolution after the 96th Done press, player 5 orders two attacks on
player 3's gangs in sector 34 and five attacks on the human's hiding gang in
sector 12. The original rolls 49 dice at `0x00475FBB` after the last
non-combat draw and before the first Hide test at `0x00473ABC`. RULE-ATTACK-001
gives 40 of them: an attack of 14 dice, a retaliation of 7 and an attack of
19, the second target being unable to strike back. With the thresholds of a
Goon attacker, those pools give the damage the end state shows on all four
gangs whether the other 9 dice come before, between or after them. The 9 add
nothing to any player's `damage_inflicted`: player 5's grows by 10, which the
two attacks and the one attack on the human that passes its Hide test account
for.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results up to call
52029, through 95 full turns of the computer players' planning and
resolution. There it rolls the first Hide test where the original rolls the
9 extra dice. The test holds the run as a known divergence at that call.

## Conclusion

The run agrees with the rebuild through 95 Siege turns at Goon and shows no
computer player ending a Siege match in that time. The 9 dice the original
rolls in the last combat phase are not explained by RULE-ATTACK-001 as
written; which gang rolls them is an open question.
