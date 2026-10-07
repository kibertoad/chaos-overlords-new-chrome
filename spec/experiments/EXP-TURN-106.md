---
id: EXP-TURN-106
title: Does a six-month Power match at Criminal end with the same scores, ranking and awards, a tie for the lead included?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-106.json
---

## Question

No recorded run plays Power to its time limit. Does a six-month Power match
at Criminal, with the human idle, end with the resolution of turn 26 with the
scores, ranking and awards the rebuild gives, also when two players share the
highest score?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-037 with Power (`--scenario 1`), Mentality 1 (`--mentality 1`),
a time limit of 26 turns (`--turns 26`) and up to thirty Done presses
(`--end-turns 30`), with no orders. Run 0 uses `--seed 9` and run 1
`--seed 8`. The seeds were chosen by playing the same settings in the
rebuild: with seed 9 two computer players end level on the highest score.

## Observations

Run 0 made 11850 calls of `roll` and run 1 made 12350, each over 26 Done
presses. At the end of both `match_over` is 1, `elapsed_turns` 25 and every
player is still active. In run 0 players 2 and 5 share the highest stored
score, and the endgame ranks player 2 above player 5; in run 1 player 2 holds
the highest score alone.

## Results

A test of the rebuild replays both runs.
The rebuild makes the same calls with the same bounds and results, ends the
match with the resolution of turn 26, and reaches the same state, stored
scores, standings, endgame rows, Last Turn reports and awards.

## Conclusion

The runs agree with RULE-OBJECTIVE-001, RULE-OBJECTIVE-002 and
RULE-OBJECTIVE-004 for the end of a Power match, and with RULE-AWARDS-001 and
RULE-AWARDS-002 for its awards and ranking.
