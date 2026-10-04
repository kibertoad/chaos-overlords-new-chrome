---
id: EXP-TURN-058
title: Does a Big Man match at Criminal end on the same turn with the same planning state and awards?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-058.json
---

## Question

No recorded run plays Big Man, whose hire table gives the computer gangs
families 13 and 14, to its end. At Criminal, does the original end the
match on the turn the rebuild ends it, with the same planning state and
awards?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Big Man (`--scenario 8`), Mentality 1
(`--mentality 1`), up to thirty Done presses (`--end-turns 30`) and
`--seed 8101`, with no orders. After the last press the probe reads the
planning state, the combat records, the combat result rows and the endgame
rows into the end state, as in EXP-TURN-048 and EXP-TURN-037.

## Observations

The run made 4662 calls of `roll` over twenty-five Done presses. At the
end `match_over` is 1 and `elapsed_turns` 24; player 2 holds a scenario
score of 40, players 4 and 1 hold 27 and 17. The endgame ranks players 2, 4,
1, 5, 0 and 3. The human holds award 4 and player 2 award 3. The planning
records of living gangs hold families 13 (12 records) and 14 (4).

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, ends the match
after the same Done press, reaches the same state and endgame rows, gives the
same awards, and holds the same planning records, sector weights,
per-player values, combat records and, for each computer gang whose family
is assigned, focus and coverage sector.

## Conclusion

The run agrees with RULE-OBJECTIVE-001, RULE-OBJECTIVE-002,
RULE-OBJECTIVE-004 and RULE-AI-031 for a whole match.
