---
id: EXP-TURN-098
title: Does the Move repair's neighbour draw from a corner in the last row draw again past sector 63?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-098.json
---
## Question

EXP-TURN-097 reaches the mode 0 selector from a corner in the first row. From
a corner in the last row, does the original also draw again for a neighbour
south of the city? The run also repeats the hire with 80 gangs
(RULE-EVENT-011).

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 6.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 6
--end-turns 82 --cash 1-82:0:30000`, with the `--hires` and `--orders` the
fixture lists as `hire` and `order` inputs. Before every Done press the probe
writes 30000 into the human's `cash` (`0x004A25E8`, FND-AI-055), so every hire
is paid for, and writes one hire order into `hire_orders` for offer slot 0
(RULE-HIRE-003), 80 in all. The orders are Moves (action 10) into a sector
holding at most five of the human's gangs, which the rebuild's order panel
accepts as well (DEV-MOVE-001). They spread the human's gangs over the city
and, in turn 32, set up the layout below. Record every random call and extract
numeric state at the 83rd planning entry.

In turn 32 the human holds five gangs in each of sectors 62 (X), 63 (Z), 61,
55 and 54, and two in each of 53, 47 and 46. Z is a corner of the city. Roster
slot 1 in Z and one gang in 61 move to X; one gang in each of 55 and 54 moves
to Z; the two gangs of 53 move to 61, those of 47 to 55 and those of 46 to 54.
Slot 1 comes before every other mover into X or Z in the roster. Counted as
RULE-MOVE-002 counts them, X then holds seven and every other sector six or
fewer, and every mover into X or Z comes from a sector counting six, so the
repair's fallback sends slot 1 back to Z, Z counts seven, and the fallback
takes slot 1 again, now with Z as its destination, and calls the mode 0
selector (RULE-AI-007).

The seed and orders were found in the rebuild, which played this plan for
seeds 1 to 30 and reached both the neighbour draw and a hire with 80 gangs in
every one; seed 6 was chosen for where its corner lies.

## Observations

The run made 50324 calls of `roll`, with the Done presses at the counts listed
in the fixture. In the resolution of turn 32 the call of `roll(8)` at
`0x0040868B`, in the mode 0 block of `fn_00408642`, was made 7 times, with
results 6, 5, 8, 5, 4, 6 and 2.

At the 83rd planning entry the human holds 80 gangs, in roster slots 0 to 79.
Its one Last Turn report (FMT-STATE-006) is type 8 with `arg1` 57, `arg2` 0
and `arg3` 0: the hire of turn 82, ordered into a sector holding fewer than
six of its gangs, found no free roster slot.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run, with
the same cash written before each Done press. The rebuild makes the same calls
with the same bounds and results, reaches the same state and builds the same
Last Turn reports for every player. In its resolution of turn 32 the Move
repair sends slot 1 back to Z and then draws its neighbours: offsets +7, +1,
+9 and +1 from sector 63 give 70, 64, 72 and 64, past the city's last sector,
and are drawn again; -1 gives sector 62, which then counts seven, so the mover
is sent back again; +7 gives 70 and is drawn again; -8 gives sector 55, whose
earlier mover from sector 47 then goes back, and the repair ends.

## Conclusion

The run agrees with RULE-AI-007 for a corner in the last row: a draw past
sector 63 is drawn again. With EXP-TURN-097 it covers the tests of the
northern, southern and eastern edges, but neither run starts in column 0, so
the test of the western edge is not reached. It repeats the type-8 report of
RULE-EVENT-011.