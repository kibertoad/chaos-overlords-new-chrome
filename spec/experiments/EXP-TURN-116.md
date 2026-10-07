---
id: EXP-TURN-116
title: Does the end-of-turn police countdown leave the island modifier's permanent Crackdowns at 100?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-116.json
---
## Question

RULE-SETUP-005 puts every unowned sector under a Crackdown of 100 turns when a
player carries the island modifier name, and RULE-POLICE-003 counts down only
a presence from 1 to 99, so 100 never changes. EXP-SETUP-004 sets the modifier
but stops at the first planning entry, before any countdown. Does the
original keep 100?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104), seed 3, and the human in slot 0 named with the island
modifier string (FND-SETUP-015), as EXP-SETUP-004 names its slot 4.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 3
--humans 0:islands --end-turns 1`, with no orders. Record every random call
and extract numeric state at the second planning entry.

## Observations

The run made 409 calls of `roll`, with the Done press at the count listed in
the fixture. At the second planning entry the unowned sectors still hold a
`crackdown_turns` of 100.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with
the same bounds and results and reaches the same state, every sector's
`crackdown_turns` included. In its replay the countdown at the end of turn 1
finds the permanent Crackdowns at 100 and leaves them there.

## Conclusion

The run agrees with RULE-POLICE-003 for a presence of 100, which the
countdown does not lower, and with RULE-SETUP-005 over a resolution.
