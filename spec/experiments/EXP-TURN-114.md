---
id: EXP-TURN-114
title: Does a Snitch that takes the base Tolerance below -128 wrap the signed byte?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-114.json
---
## Question

RULE-SNITCH-001 subtracts 3 from a sector's base Tolerance in 32 bits and
stores the low byte, so a Snitch that takes it below -128 leaves a large
positive value, which the clamp after the instant phase then lowers to 40
(RULE-TOLERANCE-002). In play that takes many Snitches in one sector in one
turn, and no run makes them. Does the original wrap the byte?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 3.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 3
--end-turns 2 --orders 1:0:13:0:0:0 --tolerance 1:54:-127`. Before the Done
press of turn 1 the probe writes a one-off Snitch (13) into the `action` of
the human's gang in roster slot 0, in sector 54, and -127 into that sector's
`base_tolerance` (FMT-STATE-002). The resolution first moves the base one
point toward 17 minus the sector's Income (RULE-TOLERANCE-001), to -126,
before the Snitch. Record every random call and extract numeric state at the
third planning entry.

## Observations

The run made 496 calls of `roll`, with the Done presses at the counts listed
in the fixture. At the third planning entry sector 54 has base Tolerance 39.

## Results

A test of the rebuild replays the run with
the same write. The rebuild makes the same calls with the same bounds and
results and reaches the same state. In its replay the Snitch of turn 1 finds
the base at -126 and stores 127; the gangs after it in the phase act with the
sector at 127, and the clamp then sets it to 40. The step of turn 2 moves it
from 40 to 39.

## Conclusion

The run agrees with RULE-SNITCH-001 for a Snitch that wraps the signed byte,
with the upper clamp of RULE-TOLERANCE-002 for the wrapped value, and with
RULE-TOLERANCE-001 for a base of 40 stepping to 39.
