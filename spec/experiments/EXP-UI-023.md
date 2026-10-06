---
id: EXP-UI-023
title: Does the victory splash look the same in the rebuild when one player is left?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-023.json
---

## Question

When a match ends with one player active, does the endgame's Awards tab draw
the same pixels in the rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --humans 0 --seed 6161 --end-turns 2
--retire 1:1,1:2,1:3,1:4,1:5 --white-key --order-steps shot:SCR-AWARDS-002`.

No local match takes five computers out of the city in a run of a few turns,
so before the first Done press the probe writes 0 into the `player_active`
bytes of players 1 to 5 (FND-STATE-004), as the elimination check does for a
player left with nothing (RULE-TURN-006). Their gangs and sectors stay where
they are. The replay takes the same players out of the rebuild's match at the
same point. The state is dumped once the endgame has drawn (FND-AWARDS-005),
and the step copies the screen.

## Observations

The run made 316 calls of `roll`, all before the Done press; the inactive
computers did not plan and the turn's resolution made none. The match ended
with turn 1, and the endgame drew the splash for player 0 alone. The shot was
kept with every light byte 0.

## Results

`TheRebuildStartsTheSameMatch` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the 316
rolls, and the endgame rows of the fixture name player 0 as the splash.
`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the frame, the tab mark,
the splash, the three colour fills, the name and the portrait of SCR-AWARDS-002
with the rebuild's endgame entered from the replayed match. No element differs.

## Conclusion

The run supports SCR-AWARDS-002 for a human survivor in slot 0, and
RULE-AWARDS-002 for opening on the splash when one player is active.
