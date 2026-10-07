---
id: EXP-TURN-019
title: Does a human gang's Terminate order retire the gang as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-019.json
---

## Question

When the human's only gang is ordered to Terminate, does the Movement phase
retire it and does the match reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004, pressing Done three times (`--end-turns 3`), with Terminate
(14) in `action` of the human's gang in roster slot 0, and 0 in
`repeat_action`, before the third press (`--orders 3:0:14:0:0:0`).

## Observations

The seed was 53179. The run made 632 calls of `roll`, 312 before the first
Done press. In the copy after the third turn the human's gang record was
inactive and player 0's `controller` was still 0.

## Results

A test of the rebuild replays the run, giving the Terminate as a command. The
rebuild makes the same calls with the same bounds and results and reaches the
same generator position and state, with the human's gang retired and the human
still in the match at the next planning phase.

## Conclusion

The run agrees with RULE-TERMINATE-001 and RULE-GANG-002.
