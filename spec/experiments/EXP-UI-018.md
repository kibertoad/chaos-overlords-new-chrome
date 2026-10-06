---
id: EXP-UI-018
title: Does the elimination card look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory until the steps after the dump, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-018.json
---

## Question

When the only local human is eliminated, does the card the original shows at
the point that player's planning would have come draw the same pixels in the
rebuild?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-017 (`--seed 44213 --orders 1:0:8:0:0:1`), with `--end-turns 31
--white-key --order-steps shot:SCR-OBJECTIVE-002`. The probe breaks at the
card's presenter `0x0042C3F5` (FND-OBJECTIVE-002); when it is reached while a
turn's Done waits for the next planning phase, the probe lets the card draw for
1.5 seconds, dumps the state and takes the shot.

## Observations

The presenter was reached after the thirtieth Done press and call 19128 of
`roll`, the count EXP-TURN-017 gives for its thirty turns, and the human's
`controller` was -2. No Ready card came before it. The shot shows the card on
black: the frame, the splash with its colour band in the human's red, the name
in the plain font and the portrait inside the splash's frame.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the capture with the
rebuild entering the replayed save, whose human is eliminated, at that
player's card. Every element matches. `TheRebuildStartsTheSameMatch` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the 19128
rolls and stops at the human's elimination.

A first comparison found the rebuild drawing the card over an empty console,
the name in the player's colour, and a border of that colour around the
portrait.

## Conclusion

The run supports SCR-OBJECTIVE-002 for the card of a single local human and
RULE-OBJECTIVE-005 for that card coming without a Ready card where the
player's planning would have come.
