---
id: EXP-TURN-103
title: Does the clamp after the instant phase bring a base Tolerance above 40 back to 40, after the later gangs have acted?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-103.json
---
## Question

RULE-TOLERANCE-002 clamps every base Tolerance to 1..40 once, after the whole
instant phase, and RULE-TURN-003 says a sector can sit above 40 while the later
gangs of the phase act. EXP-TURN-032 reaches the lower bound; no earlier run
takes a base Tolerance above 40. Does the original clamp it to 40?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 3.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 3
--end-turns 13 --cash 1-13:0:30000` and Bribe (2) given to the human's gang in
roster slot 0 before every Done press
(`--orders 1:0:2:0:0:0,2:0:2:0:0:0,...,13:0:2:0:0:0`, one order for each of
turns 1 to 13). Before every Done press the probe writes 30000 into the
human's `cash` (FND-AI-055), so every Bribe is paid for. The gang stays in its
starting sector 54, which the human owns, with Income 3. Record every random
call and extract numeric state at the 14th planning entry.

The seed was found in the rebuild, which played this plan for seeds 1 to 40;
in every one a Bribe takes the base Tolerance above 40 between turns 13 and
15, and seed 3 does it first, in turn 13.

## Observations

The run made 2373 calls of `roll`, with the Done presses at the counts listed
in the fixture. At the 14th planning entry sector 54 had base Tolerance 40.

## Results

A test of the rebuild replays the run, with
the same cash written before each Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state. In its replay the
base Tolerance of sector 54 before each Bribe of turns 1 to 13 is 14, 16, 18
and so on up to 38: each resolution first moves it one point toward
17 - 3 = 14 (RULE-TOLERANCE-001) and the Bribe adds 3. The Bribe of turn 13
takes it to 41, the gangs after it in the phase act with the sector at 41,
among them computer gangs' Influence, Research and Heal, and the clamp then
sets it to 40.

## Conclusion

The run agrees with the upper bound of RULE-TOLERANCE-002 and with
RULE-TURN-003: the base Tolerance stays above 40 until every gang of the
phase has acted, and the clamp then lowers it to 40.
