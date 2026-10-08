---
id: EXP-TURN-107
title: Does a Big 40 match at Goon end on the turn a computer player takes its fortieth sector?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-107.json
---

## Question

No recorded run ends a Big 40 match on its objective, and no run compares
the Big 40 scores. A recorded run of the original stops when its only human
is eliminated (RULE-OBJECTIVE-005), so a computer player's win can be
recorded only while the human survives. Does a Big 40 match at Goon, with the
human hiding, end on the turn the rebuild ends it, with the same scores and
awards?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-037 with Big 40 (`--scenario 5`), Mentality 0 (`--mentality 0`),
`--seed 19` and up to 95 Done presses (`--end-turns 95`). Before the first
Done press the human's gang in roster slot 0 is set to Hide with Hide as its
recurring order (`--orders 1:0:8:0:0:1`), as in EXP-TURN-039, so it hides in
every turn. The seed was chosen by playing the same settings in the rebuild,
which ends the match after 88 turns with the human still in it.

## Observations

The run made 68150 calls of `roll` over 88 Done presses. At the end
`match_over` is 1 and `elapsed_turns` 87; player 4 holds a stored score of 41
and players 1 to 3 hold 9, 11 and 3, and every player is still active. The
endgame ranks players 4, 2, 1, 3, 0 and 5. In the last resolution player 4's
gangs complete two sites, and its six Last Turn reports begin with the two
site reports.

## Results

A test of the rebuild replays the run. The
rebuild makes the same calls with the same bounds and results, ends the match
after the same Done press, and reaches the same state, stored scores,
standings, endgame rows, Last Turn reports and awards. Before the rebuild kept every notification
of the turn just completed, it lost player 4's two site reports: its
notification history held 64 entries per player and dropped the oldest,
and player 4's gangs gave more than 64 notifications in that resolution.

## Conclusion

The run agrees with RULE-OBJECTIVE-001, RULE-OBJECTIVE-002 and
RULE-OBJECTIVE-004 for a Big 40 match ended on its objective, with
RULE-AWARDS-001 and RULE-AWARDS-002, and with RULE-EVENT-002 and
RULE-EVENT-006 for the reports of a player with many gangs.
