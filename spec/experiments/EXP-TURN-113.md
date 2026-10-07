---
id: EXP-TURN-113
title: Does a Kill 'Em All match at Goon run as the rebuild runs it for 150 turns?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-113.json
---

## Question

No recorded run plays Kill 'Em All beyond turn 30. Kill 'Em All ends only when
one player is left (RULE-OBJECTIVE-001), and with one human in the match a
recorded run cannot see a computer player win it unless every other player,
the human included, is eliminated. Does a Kill 'Em All match at Goon, with the
human hiding, make the same draws as the rebuild over 150 turns?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-107 with Kill 'Em All (`--scenario 4`), `--seed 21` and 150 Done
presses (`--end-turns 150`). The human's gang in roster slot 0 hides in every
turn (`--orders 1:0:8:0:0:1`). The seed was chosen by playing the same
settings in the rebuild, where the human survives 200 turns and the match
does not end.

## Observations

The run made 108911 calls of `roll` over 150 Done presses. At the end
`elapsed_turns` is 150 and all six players are still active, so the match
has not ended.

The resolution after the 116th Done press ends with a draw at `0x00475AC6`,
the Force of a hired gang (FND-RNG-006), before the next planning entry's
offer draw at `0x0047172A`.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the same bounds and results up to call
96038, through 115 full turns and the planning and resolution of the 116th
up to its hire phase. No computer player queues a hire in the rebuild in that
turn: the planner of each one rejects an offer instead, player 2 with 0 cash.
The rebuild therefore makes the next offer draw where the original rolls the
hired gang's Force. The test holds the run as a known divergence at that
call.

## Conclusion

The run agrees with the rebuild through 115 Kill 'Em All turns at Goon and
shows no player eliminated in 150 turns. A computer player hires in the
116th turn of the original where the rebuild's planner rejects every offer;
which player hires, and why, is an open question.
