---
id: EXP-TURN-050
title: Does a family-7 Equip leave the focus its handler compares at the next pass?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-050.json
---

## Question

FND-AI-074 reads family 7's Equip branches as storing no focus, so the focus
stays what the pass before left. Does the original hold that focus after a
family-7 gang plans an Equip?

## Setup

As EXP-TURN-031: seed 1, the setup screen's defaults.

## Procedure

As EXP-TURN-044 without the Financial panel presses:

`--seed 1 --end-turns 10 --orders 1:0:5:24:0:0,2:1:5:24:0:0,3:0:2:0:0:0,4:0:2:0:0:0,5:0:2:0:0:0,6:1:5:40:0:0,7:1:5:0:0:0,8:0:5:0:0:0,8:1:12:2:0:0,9:0:12:2:0:0,9:1:5:24:0:0,10:0:3:0:0:0,10:1:10:52:0:0 --hires 1:0:51`

The probe reads the planning state into the end state as in EXP-TURN-048.

## Observations

The run made 2320 calls of `roll`. At the end, auxiliary record 328, a
family-7 gang that planned Equip, holds focus 13.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, and holds the same planning record bytes, sector weights,
per-player values and, for each computer gang whose family is assigned, focus
and coverage sector.

## Conclusion

The run agrees with FND-AI-074: family 7's Equip keeps the focus of the pass
before. A rebuild that cleared it to -1 there holds -1 in record 328.
