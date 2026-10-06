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
sector 12. Its gang in roster slot 19 Influences site 2 of sector 25 in the
instant phase, a pool of 9 dice (RULE-INFLUENCE-001); FND-AI-081 traces the
choice to player 5's cash after an Equip of the 94th turn.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same 52052 calls with the same bounds and results, and its
end state agrees with the original's.

## Conclusion

The run agrees with the rebuild through 96 Siege turns at Goon and shows no
computer player ending a Siege match in that time.
