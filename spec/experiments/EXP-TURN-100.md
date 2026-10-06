---
id: EXP-TURN-100
title: Does the Move repair's neighbour draw from corner sector 56 draw again when only the test of the western edge refuses the result?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-100.json
---

## Question

EXP-TURN-099 draws from corner sector 0, where every offset the western test
refuses also leaves the range 0 to 63. From sector 56, in column 0 of the
last row, offset -1 gives sector 55 and +7 gives sector 63: both are in the
city, and only the test of the western edge refuses them (FND-MOVE-003). Does
the original draw again for them? The run also repeats the hire with 80 gangs
(RULE-EVENT-011).

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 7.

## Procedure

As EXP-TURN-097, with `--seed 7` and the `--hires` and `--orders` the fixture
lists. In turn 34 the human holds five gangs in each of sectors 57 (X), 56
(Z), 58, 48 and 49, and two in each of 50, 40 and 41. Roster slot 6 in Z and
one gang in 58 move to X; one gang in each of 48 and 49 moves to Z; the two
gangs of 50 move to 58, those of 40 to 48 and those of 41 to 49. Slot 6 comes
before every other mover into X or Z in the roster, so the repair sends it
back to Z and then calls the mode 0 selector for it (RULE-AI-007).

The seed was found in the rebuild, which played this plan for seeds 1 to 60
and logged every neighbour draw; seed 7 is one whose draws from sector 56 include
both offsets the western test alone refuses. As in EXP-TURN-097 the
probe presses Exit on each Combat Results panel, and the run took about 20
minutes.

## Observations

The run made 57888 calls of `roll`, with the Done presses at the counts listed
in the fixture. In the resolution of turn 34 the call of `roll(8)` at
`0x0040868B`, in the mode 0 block of `fn_00408642`, was made 5 times, with
results 4, 4, 7, 6 and 3.

At the 83rd planning entry the human holds 80 gangs, in roster slots 0 to 79,
and its Last Turn reports include one of type 8 (FMT-STATE-006): the hire of
turn 82 found no free roster slot.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run, with
the same cash written before each Done press. The rebuild makes the same calls
with the same bounds and results, reaches the same state, builds the same Last
Turn reports for every player and shows the same panels at each planning entry.
In its resolution of turn 34 the Move repair sends slot 6 back to Z and draws
from sector 56: offset -1 gives sector 55 and is drawn again, -1 again gives
55, +8 gives 64, outside the city, +7 gives 63 and is drawn again, and -7
gives sector 49, which is kept.

## Conclusion

The run agrees with RULE-AI-007 for a corner in column 0 of the last row.
Three draws, two of offset -1 and one of +7, land inside the city and are
refused by the test of the western edge alone, so that test is reached and
the original draws again, as FND-MOVE-003 reads. It repeats the type-8 report
of RULE-EVENT-011.
