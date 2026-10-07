---
id: EXP-TURN-090
title: Does the original carry out a computer player's hire into a sector it neither controls nor holds a gang in?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-090.json
---

## Question

A human can hire only into a sector they control or hold a gang in. The hire
resolver of RULE-HIRE-001 tests the sector's room and the player's cash but
not its owner. When the computer planner places a hire in a sector the player
neither controls nor holds a gang in, does the original carry it out?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Big Man (`--scenario 8`), Mentality 3 (`--mentality 3`),
twenty-four Done presses (`--end-turns 24`) and `--seed 9`. The human gives no
orders. Before the twentieth Done press the probe writes 5 into the `family`
of the planning records in every roster slot below 10 of the computer players
that held a gang at the time (FMT-STATE-007, `--families
20:1:1:5,20:1:2:5,20:2:0:5,20:2:1:5,20:2:2:5,20:2:3:5,20:2:4:5,20:2:5:5,20:3:1:5,20:3:3:5,20:4:0:5,20:4:1:5,20:4:2:5,20:4:3:5,20:4:4:5,20:4:5:5,20:4:6:5,20:4:7:5,20:4:8:5,20:4:9:5,20:5:0:5,20:5:1:5`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 20, with families 0, 4, 5 and 7 written into every
computer gang at turn 20 or 30, and recorded which of a list of unreached
planner branches each match took.

## Observations

The run made 3909 calls of `roll` over twenty-four Done presses. At the end
`elapsed_turns` is 24 and no player's `controller` has changed. After the
twentieth Done press player 3 hires into sector 36, which it does not control
and holds no gang in. The original calls `roll(5)` at `0x00475AC6` for the new
gang's Force (RULE-HIRE-001), and at the next planning entry player 3 holds a
new gang in sector 36 and its hire offer is marked taken (FMT-STATE-001).

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off and writes
the same families before the same Done press. With DEV-AI-008 switched off the
rebuild makes the same calls with the same bounds and results and reaches the
same state, the planning records included. With it on, the rebuild drops that
hire, makes no Force roll and goes out of step at call 3335, which the replay
test checks.

## Conclusion

The original carries out a computer player's hire into a sector the player
neither controls nor holds a gang in, as RULE-HIRE-001 gives with no owner
test (DEV-AI-008).
