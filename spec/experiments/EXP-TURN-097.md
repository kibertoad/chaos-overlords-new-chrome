---
id: EXP-TURN-097
title: Does the Move repair draw a random neighbour for a mover already sent back, and does a hire with 80 gangs report a full roster?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-097.json
---
## Question

When a mover sent back to its own sector leaves that sector crowded and no
other mover into it can go back, does the original call the mode 0 selector,
and how does the draw treat the sectors off the map? And when a player holding
80 gangs hires again, does the original report the full roster to that player
(RULE-EVENT-011)?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 11.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 11
--end-turns 82 --cash 1-82:0:30000`, with the `--hires` and `--orders` the
fixture lists as `hire` and `order` inputs. Before every Done press the probe
writes 30000 into the human's `cash` (`0x004A25E8`, FND-AI-055), so every hire
is paid for, and writes one hire order into `hire_orders` for offer slot 0
(RULE-HIRE-003), 80 in all. The orders are Moves (action 10) into a sector
holding at most five of the human's gangs, which the rebuild's order panel
accepts as well (DEV-MOVE-001). They spread the human's gangs over the city
and, in turn 34, set up the layout below. Record every random call and extract
numeric state at the 83rd planning entry.

In turn 34 the human holds five gangs in each of sectors 6 (X), 7 (Z), 5, 15
and 14, and two in each of 13, 23 and 22. Z is a corner of the city. Roster
slot 6 in Z and one gang in 5 move to X; one gang in each of 15 and 14 moves
to Z; the two gangs of 13 move to 5, those of 23 to 15 and those of 22 to 14.
Slot 6 comes before every other mover into X or Z in the roster. Counted as
RULE-MOVE-002 counts them, X then holds seven and every other sector six or
fewer, and every mover into X or Z comes from a sector counting six, so the
repair's fallback sends slot 6 back to Z, Z counts seven, and the fallback
takes slot 6 again, now with Z as its destination, and calls the mode 0
selector (RULE-AI-007).

The seed and orders were found in the rebuild, which played this plan for
seeds 1 to 30 and reached both the neighbour draw and a hire with 80 gangs in
every one; seed 11 was chosen for where its corner lies.

## Observations

The run made 55405 calls of `roll`, with the Done presses at the counts listed
in the fixture. In the resolution of turn 34 the call of `roll(8)` at
`0x0040868B`, in the mode 0 block of `fn_00408642`, was made 9 times, with
results 8, 8, 1, 3, 3, 1, 5, 4 and 7.

At the 83rd planning entry the human holds 80 gangs, in roster slots 0 to 79.
Its one Last Turn report (FMT-STATE-006) is type 8 with `arg1` 48, `arg2` 0
and `arg3` 0: the hire of turn 82, ordered into a sector holding fewer than
six of its gangs, found no free roster slot.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run, with
the same cash written before each Done press. The rebuild makes the same calls
with the same bounds and results, reaches the same state and builds the same
Last Turn reports for every player. In its resolution of turn 34 the Move
repair sends slot 6 back to Z and then draws its neighbours: offsets +9, +9,
-9, -7, -7, -9 and +1 from sector 7 give 16, 16, -2, 0, 0, -2 and 8, which
leave the city or wrap past its eastern edge and are drawn again; -1 gives
sector 6, which then counts seven, so slot 6 is sent back to sector 7 again;
+7 gives sector 14, whose earlier mover from sector 22 then goes back, and the
repair ends.

## Conclusion

The run agrees with RULE-AI-007 and RULE-MOVE-002: the fallback calls the mode
0 selector for a mover already sent back, a draw north of the city or past its
eastern edge is drawn again, a draw that crowds a sector is repaired in a
later round, and the drawn sector is not tested for room. It agrees with
RULE-HIRE-001 and RULE-EVENT-011: a hire with 80 gangs makes its Force draw
and gives its player a type-8 report. The corner is in column 7 and row 0, so
the run does not reach the test of the western edge or the southern one.