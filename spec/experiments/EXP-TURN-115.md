---
id: EXP-TURN-115
title: Does a base Tolerance held at 40 step to 39 each resolution and return to 40 with a Bribe?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-115.json
---
## Question

RULE-TOLERANCE-001 moves every base Tolerance one point toward 17 minus the
sector's Income at the start of each resolution, so a sector held at 40 by
Bribes steps to 39 before the Bribe of the turn, which takes it to 42, and the
clamp returns it to 40. EXP-TURN-103 ends at the first clamp to 40. Does the
original repeat this turn after turn?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 3.

## Procedure

As EXP-TURN-103 with fifteen Done presses: `--scenario 0 --mentality 0
--turns 104 --seed 3 --end-turns 15 --cash 1-15:0:30000` and a one-off Bribe
(2) given to the human's gang in roster slot 0 before every Done press
(`--orders 1:0:2:0:0:0,...,15:0:2:0:0:0`, one order for each of turns 1 to
15). Record every random call and extract numeric state at the 16th planning
entry.

## Observations

The run made 3064 calls of `roll`, with the Done presses at the counts listed
in the fixture. At the 16th planning entry sector 54 has base Tolerance 40.

## Results

A test of the rebuild replays the run, with
the same cash written before each Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state. In its replay the
Bribe of turn 13 takes the base to 41 and the clamp to 40, as in EXP-TURN-103.
In turns 14 and 15 the step moves it from 40 to 39, the Bribe takes it to 42,
and the clamp sets it to 40.

## Conclusion

The run agrees with RULE-TOLERANCE-001 for a base of 40 stepping to 39 and
returning to 40 with one Bribe, and with the upper clamp of
RULE-TOLERANCE-002.
