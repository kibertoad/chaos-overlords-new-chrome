---
id: EXP-UI-014
title: Does the rebuild mark the objective sectors of Big Man on the city map as the original does?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-014.json
---

## Question

At the second planning entry of a Big Man match, scenario 8, does the city
screen draw the pylons of RULE-UI-012 on the four centre sectors 27, 28, 35
and 36, and the rest of the city screen, with the same pixels in the rebuild
as in the original?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 8 --mentality 1 --seed 3
--end-turns 1 --white-key --order-steps exit,exit,shot:SCR-UI-003`. The two
`exit` steps close the panels the planning entry opened, when it opened any.
The shot is taken and its counters read as in EXP-UI-009.

## Observations

The run made 347 calls of `roll`. The shot was kept at step 2, with marker
frame 4, pump counter 1, frame counter 1, sector 54 selected and every light
byte 0. The capture shows two gray pylons on the four centre sectors 27, 28,
35 and 36.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the capture as for
EXP-UI-009. Leaving out the cash row (DEV-UI-006) and the city's key line
(DEV-UI-023), every element matches, the map with its pylons included.

## Conclusion

The run supports RULE-UI-012 for Big Man and SCR-UI-003 for one more state.
