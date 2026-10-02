---
id: EXP-TURN-046
title: How does the planning clock run out with a limit of 30 seconds?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11, staged original executable and SMACKW32.DLL with junctions to original assets, windowed under the debugging probe, seed 1
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-046.json
---

## Question

With a planning time limit of 30 seconds, what limit does the match store, how
often is the clock bar redrawn, with which width and warning sound, and at
which elapsed time does the turn end?

## Setup

As EXP-TURN-031: seed 1, the setup screen's defaults, a hash-verified
BLD-GOG-EN-1.1, with `planning_limit_choice` set to 1.

## Procedure

Run the probe with `--seed 1 --end-turns 2 --time-limit 1 --expire-turns 1,2`. The probe writes `planning_limit_choice`
before Begin and presses no Done in the listed turns. It breaks where the start
helper stores `planning_start_ms`, where the drawing helper has stored the
width, on the sound wrapper called from the drawing helper, and in the
time-limit test (FND-TIMER-001, FND-TIMER-003). Record every random call and
extract numeric state.

## Observations

The run made 509 calls of `roll`. In turn 1 the stored limit was 30000 ms. The bar was redrawn 31 times, first at 0 ms, then every 972 to 1021 ms after the second redraw; 9 redraws played slot 7 and 1 slot 8. The last test that let planning go on saw 29988 ms and the one that ended it 30005 ms. In turn 2 the stored limit was 30000 ms. The bar was redrawn 31 times, first at 0 ms, then every 986 to 1007 ms after the second redraw; 9 redraws played slot 7 and 1 slot 8. The last test that let planning go on saw 29999 ms and the one that ended it 30016 ms.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches the
same state, with each expired turn taken as a Done press. For the same choice
it stores the same limit, for each recorded elapsed time it draws the same
width and plays the same warning slot, and it lets planning go on and end at
the same elapsed times.

## Conclusion

The run agrees with RULE-TIMER-001, RULE-TIMER-002 and RULE-TIMER-003. The
redraws come about once a second, every sixth tick of the presentation clock,
and the turn ends on the first test whose elapsed time is above the limit.
