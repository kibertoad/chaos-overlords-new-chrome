---
id: EXP-VIDEO-001
title: How many steps does the intro show of each movie when it plays out?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-VIDEO-001.json
---

## Question

When neither intro movie is skipped, in which order does the intro play them,
how many times does the frame helper show a frame of each, and how far apart?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --humans 0 --seed 8383 --end-turns 0
--watch-intro --timeout 400`.

The probe holds no button until both movies have been closed. It sets
breakpoints on the frame helper's call of `SmackDoFrame` at `0x0040DEF0` and on
the close helper `fn_0040DD7B` (FND-VIDEO-002). At each frame it reads the
movie name the intro copied to `0x00498762`, the frame counter of movie slot 0
at `0x004905CC`, and, for a new movie, the frame count of the movie's header
at offset `0xC` of the `Smack` handle at `0x004905D0`, and it times the frame
from the first. At the close it reads the counter again. Then the run goes on
to a new match as EXP-TURN-001 does.

## Observations

The logos movie played first and the intro movie second. The headers gave 200
and 1150 frames, as FND-VIDEO-001 reads them from the files. The helper showed
the logos movie 201 times and the intro movie 1151 times. The slot's counter
ran from 0 to the frame count, and the slot was closed with the counter at the
frame count. The last step of each came 19906 ms and 114909 ms after its first,
99.5 and 99.9 ms a step, and the intro movie's first step came 43 ms after the
logos movie's last.

## Results

`TheIntroPlaysEachMovieToTheStepAfterItsLastFrame` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Intro.cs` checks these
observations and steps the rebuild's movie timeline for the same frame counts
at 100 ms: it decodes each frame once and ends the movie one step after the
last, n + 1 steps in all, so the last frame stays up for 100 ms as in the
original.

## Conclusion

The run supports RULE-VIDEO-001 for movies played out: each movie of n frames
is stepped n + 1 times, 100 ms apart, the last step wrapping to the first frame
as the files have no ring frame (FND-VIDEO-001), and the movie is closed at that
step, so its last frame stays up for 100 ms. The run does not press the button.
