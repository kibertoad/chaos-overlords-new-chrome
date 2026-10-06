---
id: EXP-TURN-110
title: Does a Heal by a gang at Force 10 roll its pool and leave the Force at 10?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-110.json
---
## Question

RULE-HEAL-001 rolls the whole pool of a healing gang even when the gang is
already at Force 10, and the cap then keeps it at 10 (FND-HEAL-001). Play does
not give the case: the order menus and the group bar refuse Heal at Force 10
and a recurring Heal is cleared at `turn_start` (FND-HEAL-002, FND-UI-021,
FND-TURN-004), so a gang meets it only if its Force rises after the order was
given. Does the original roll the pool?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 3.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 3
--end-turns 2 --force 1:0:0:9,2:0:0:10 --orders 2:0:7:0:0:0`. Before the Done
press of turn 1 the probe writes 9 into the `force` of the human's gang in
roster slot 0, the Right Hands in sector 54 (FMT-STATE-001). Before the Done
press of turn 2 it writes a one-off Heal (7) into that gang's `action` and
then 10 into its `force`, so the gang is at Force 10 when the instant phase
reaches it, as if its Force had risen after the order. Record every random
call and extract numeric state at the third planning entry.

## Observations

The run made 500 calls of `roll`, with the Done presses at the counts listed
in the fixture. At the third planning entry the gang is at Force 10.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild takes the Heal while the gang is at Force 9 and then writes Force 10,
as the probe does. It makes the same calls with the same bounds and results
and reaches the same state. In its replay the Heal of turn 2 acts at Force 10,
rolls a pool of 4 dice, scores 2 successes and leaves the Force at 10.

## Conclusion

The run agrees with RULE-HEAL-001 and FND-HEAL-001 for a Heal by a gang
already at Force 10: the pool is rolled and its draws are used, and the cap
keeps the Force at 10.
