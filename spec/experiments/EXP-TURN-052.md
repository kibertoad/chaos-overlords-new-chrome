---
id: EXP-TURN-052
title: How does the planning clock run out with a limit of 5 minutes?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11, staged original executable and SMACKW32.DLL with junctions to original assets, windowed under the debugging probe, seed 1
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-052.json
---

## Question

With a planning time limit of 5 minutes, what limit does the match store, how
often is the clock bar redrawn, with which width and warning sound, and at
which elapsed time does the turn end?

## Setup

As EXP-TURN-031: seed 1, the setup screen's defaults, a hash-verified
BLD-GOG-EN-1.1, with `planning_limit_choice` set to 3.

## Procedure

Run the probe with `--seed 1 --end-turns 1 --time-limit 3 --expire-turns 1`. The probe writes `planning_limit_choice`
before Begin and presses no Done in the listed turns. It breaks where the start
helper stores `planning_start_ms`, where the drawing helper has stored the
width, on the sound wrapper called from the drawing helper, and in the
time-limit test (FND-TIMER-001, FND-TIMER-003). The breakpoints are `0x0041B8C8`
in the start helper `fn_0041B8BC`, the store of `timer_ms` in
`planning_start_ms`; `0x0041B96D` in the drawing helper `fn_0041B8FC`, after the
width is stored, with `elapsed * 100` at `[ebp-4]` and the width at `[ebp-8]`;
`fn_00464290` when it returns between `0x0041B8FC` and `0x0041BCBB`, with the
effect slot as its first argument; `0x0041BDFD`, the time-limit test's compare
with the elapsed time in `eax`; and `0x0041BE09`, which runs only when the limit
has passed. Record every random call and extract numeric state.

## Observations

The run made 407 calls of `roll`. In turn 1 the stored limit was 300000 ms. The bar was redrawn 302 times, first at 0 ms, then every 966 to 1022 ms after the second redraw; 9 redraws played slot 7 and 1 slot 8. The last test that let planning go on saw 299991 ms and the one that ended it 300008 ms.

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
