---
id: EXP-TURN-069
title: Does an Influence with a pool of 0 or less roll nothing and leave the site unchanged, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-069.json
---

## Question

No other recorded run orders an Influence by a gang whose Force plus effective
Influence is 0 or less. Does such an Influence roll no dice and leave the
site's progress where it was, as RULE-INFLUENCE-001 gives for the human's
band 1?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
three Done presses (`--end-turns 3`) and `--seed 9`. In turn 1 write a hire
order for offer slot 2 into sector 51, the headquarters sector
(`--hires 1:2:51`); the offer holds a gang of Force 4 and Influence -6
(FMT-DATA-001). Before turn 2, write a one-off Influence (9) of site slot 1 by
roster slot 1, the hired gang, into its `action` and `target`
(`--orders 2:1:9:1:0:0`).

## Observations

The run made 557 calls of `roll` over three Done presses. At the end
`elapsed_turns` is 3 and every player is still active. The human's
`difficulty_band` is 1.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off. The rebuild makes the same calls with the same bounds
and results and reaches the same state, sites included. An Influence that
rolled dice for a pool of 0 or less would have added calls of `roll(6)` in
turn 2.

## Conclusion

The run agrees with RULE-INFLUENCE-001 for a pool of 0 or less at band 1,
which rolls nothing and leaves the site's progress unchanged.
