---
id: EXP-TURN-014
title: Do twenty-five turns of a new local Big Man game draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-014.json
---

## Question

As EXP-TURN-010, in Big Man (scenario 8), where the computer players' gangs
take families 13 and 14 and the Big Man points are drawn: do the turns make
the draws and reach the state the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-010, with Big Man chosen on the setup screen (`--scenario 8`),
pressing Done twenty-five times, with Hide (8) in `action` and
`repeat_action` of the human's gang in roster slot 0 before the first press
(`--orders 1:0:8:0:0:1`).

## Observations

The seed was 10857. The run made 7028 calls of `roll`, 314 before the first
Done press. The copy held 25 in `elapsed_turns`, every player active, and the
human's gang in roster slot 0.

At the end the six players held 0, 2, 2, 0, 1 and 1 Last Turn reports
(FMT-STATE-006): a Hire refused because its sector was full for player 1 in
sector 36 and for player 4 in sector 35 (type 7), three completed sites
(type 4), and player 5's Control of sector 42, owned by no one before (type 2
with `arg2` -1).

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results, reaches the same generator position and state, and
builds the same Last Turn reports for every player.

## Conclusion

The run agrees with the spec over twenty-five turns of Big Man.
