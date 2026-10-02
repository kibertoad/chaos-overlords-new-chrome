---
id: EXP-TURN-024
title: Do family-1 gangs attack and snitch at Mentality Crime Lord as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-024.json
---

## Question

At Crime Lord a family-1 gang whose previous action was Attack, Hide or Move
passes the Snitch gate in a sector whose owner reads as human
(RULE-AI-020), and one in a sector of weight 10 draws an attack target. Over
twenty-nine turns of Power at Crime Lord with an idle human, do those branches
play out as the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 15135`, Power (`--scenario 1`), Mentality 2
(`--mentality 2`), one year (`--turns 52`) and twenty-nine Done presses
(`--end-turns 29`), with no orders. The seed was found as in EXP-TURN-023. A
run of thirty presses was also made: in its thirtieth turn player 1's gang in
sector 0 was given Move to sector 2, two steps away, and ended there, which
the rebuild refuses (DEV-AI-007), so the recorded run stops a turn earlier.

## Observations

The seed was 15135. The run made 16393 calls of `roll`, 317 before the first
Done press.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state. In the replay, family-1 gangs plan Attack
after Move and after Attack, and Snitch after Move, after Control and after
Snitch.

## Conclusion

The run agrees with the family-1 branches of RULE-AI-020 it reaches.
