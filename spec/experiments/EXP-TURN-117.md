---
id: EXP-TURN-117
title: Do a Snitch and a Bribe in one sector net out before the clamp, and is a sector no gang acted in clamped?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-117.json
---
## Question

RULE-TOLERANCE-002 clamps every base Tolerance to 1..40 once, after the whole
instant phase, so a Snitch and a Bribe in one sector in one turn net out
before it, and a sector out of range is clamped even when no gang acted in it.
No earlier run has both orders in one sector in one turn, and none has a
sector out of range at the start of a phase. Does the original do both?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 3.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 3
--end-turns 2 --cash 1:0:30000 --hires 1:0:54 --orders
2:0:13:0:0:0,2:1:2:0:0:0 --tolerance 2:0:60,2:7:-20`. Before the Done press of
turn 1 the probe writes 30000 into the human's `cash` (FND-AI-055) and hires
the gang of offer slot 0 into sector 54, where the Right Hands stand; it takes
roster slot 1. Before the Done press of turn 2 it writes a one-off Snitch (13)
for slot 0 and a one-off Bribe (2) for slot 1, and writes 60 into the
`base_tolerance` of sector 0 and -20 into that of sector 7 (FMT-STATE-002), two
sectors in which no gang acts that turn. Record every random call and extract
numeric state at the third planning entry.

## Observations

The run made 503 calls of `roll`, with the Done presses at the counts listed
in the fixture. At the third planning entry sector 54 has base Tolerance 14,
sector 0 has 40 and sector 7 has 1.

## Results

A test of the rebuild replays the run with the same writes. The rebuild makes
the same calls with the same bounds and results and reaches the same state. In
its replay the step of turn 2 leaves sector 54 at 14, moves sector 0 to 59 and
sector 7 to -19; the Snitch takes sector 54 to 11 and the Bribe back to 14; no
gang acts in sector 0 or 7, and the clamp sets them to 40 and 1.

## Conclusion

The run agrees with RULE-TOLERANCE-002: a Snitch and a Bribe in one sector net
out before the clamp, and the clamp reaches sectors in which no gang acted.
