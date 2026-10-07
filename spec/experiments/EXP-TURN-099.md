---
id: EXP-TURN-099
title: Does the Move repair's neighbour draw from a corner in column 0 draw again past the western edge?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-099.json
---

## Question

EXP-TURN-097 and EXP-TURN-098 reach the mode 0 selector from corners in column
7. From corner sector 0, does the original also draw again for a neighbour
past the western edge? The run also repeats the hire with 80 gangs
(RULE-EVENT-011).

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 14.

## Procedure

As EXP-TURN-097, with `--seed 14` and the `--hires` and `--orders` the fixture
lists. In turn 32 the human holds five gangs in each of sectors 1 (X), 0 (Z),
2, 8 and 9, and two in each of 10, 16 and 17. Roster slot 1 in Z and one gang
in 2 move to X; one gang in each of 8 and 9 moves to Z; the two gangs of 10
move to 2, those of 16 to 8 and those of 17 to 9. Slot 1 comes before every
other mover into X or Z in the roster, so the repair sends it back to Z and
then calls the mode 0 selector for it (RULE-AI-007).

Computer players attack the human's gangs often in the second half of the run,
so the original shows Combat Results at many planning entries; the probe
presses Exit until each closes, and the run took about 30 minutes.

## Observations

The run made 64089 calls of `roll`, with the Done presses at the counts listed
in the fixture. In the resolution of turn 32 the call of `roll(8)` at
`0x0040868B`, in the mode 0 block of `fn_00408642`, was made 7 times, with
results 4, 1, 3, 4, 4, 5 and 8.

At the 83rd planning entry the human holds 80 gangs, in roster slots 0 to 79.
Its one Last Turn report (FMT-STATE-006) is type 8 with `arg1` 14, `arg2` 0 and
`arg3` 0: the hire of turn 82, ordered into a sector holding fewer than six of
its gangs, found no free roster slot.

## Results

A test of the rebuild replays the run, with the same cash written before each
Done press. The rebuild makes the same calls with the same bounds and results,
reaches the same state, builds the same Last Turn reports for every player and
shows the same panels at each planning entry. In its resolution of turn 32 the
Move repair sends slot 1 back to Z and draws: offsets -1, -9, -7, -1 and -1 from
sector 0 leave the city and are drawn again; +1 gives sector 1, which then
counts seven, so slot 1 is sent back to sector 0 again; +9 gives sector 9, whose
earlier mover from sector 17 then goes back, and the repair ends.

## Conclusion

The run agrees with RULE-AI-007 for a corner in column 0: offsets -1, -9 and
-7 are drawn again. Each of them gives a sector below 0, which the loop head's
test of the range 0 to 63 refuses as well as the edge tests (FND-MOVE-003).
The one offset from sector 0 that only the test of the western edge refuses,
+7 to sector 7, was not drawn, so a draw that test alone refuses is still not
reached. It repeats the type-8 report of RULE-EVENT-011.
