---
id: EXP-TURN-111
title: Does a Bribe that takes the base Tolerance past 127 wrap the signed byte?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-111.json
---
## Question

RULE-BRIBE-001 adds 3 to a sector's base Tolerance in 32 bits and stores the
low byte, so a Bribe that takes it above 127 leaves a negative value, which
the clamp after the instant phase then raises to 1 (RULE-TOLERANCE-002). The
base is at most 40 when a phase begins, so in play only the thirtieth Bribe in
one sector in one turn wraps it, and no run makes thirty. Does the original
wrap the byte?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 3.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 3
--end-turns 2 --orders 1:0:2:0:0:0 --tolerance 1:54:127`. Before the Done
press of turn 1 the probe writes a one-off Bribe (2) into the `action` of the
human's gang in roster slot 0, in sector 54, and 127 into that sector's
`base_tolerance` (FMT-STATE-002). The resolution first moves the base one
point toward 17 minus the sector's Income (RULE-TOLERANCE-001), to 126, before
the Bribe. Record every random call and extract numeric state at the third
planning entry.

## Observations

The run made 496 calls of `roll`, with the Done presses at the counts listed
in the fixture. At the third planning entry sector 54 has base Tolerance 2.

## Results

A test of the rebuild replays the run with the same write. The rebuild makes
the same calls with the same bounds and results and reaches the same state. In
its replay the Bribe of turn 1 finds the base at 126 and the human at 20 cash,
pays 3 and stores -127; the gangs after it in the phase act with the sector at
-127, and the clamp then sets it to 1. The step of turn 2 moves it to 2.

## Conclusion

The run agrees with RULE-BRIBE-001 for a Bribe that wraps the signed byte,
and with the lower clamp of RULE-TOLERANCE-002 for the wrapped value.
