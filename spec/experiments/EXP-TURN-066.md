---
id: EXP-TURN-066
title: In Power, does Chaos in a sector under police presence crack down again, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-066.json
---

## Question

As EXP-TURN-064 in Power: does a sector under police presence crack down again
when the Chaos of the human's gangs there exceeds its Tolerance?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-064 with Power (`--scenario 1`).

## Observations

The run made 1445 calls of `roll` over seven Done presses. At the end
`elapsed_turns` is 7 and every player is still active. Sector 51 is neutral
and holds `crackdown_turns` 7. The human's cash is -33.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state. In the replay, sector 51 cracks down
in turns 6 and 7 while it is under police presence.

An eighth Done press eliminates the human in the original and in the rebuild,
after 1704 calls of `roll`, so the run stops at seven presses.

## Conclusion

The run agrees with RULE-CHAOS-001 in a sector under police presence: no test
of presence is made, so the sector cracks down again.
