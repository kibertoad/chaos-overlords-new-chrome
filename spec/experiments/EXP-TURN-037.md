---
id: EXP-TURN-037
title: How does a six-month Greed end, and which awards does the endgame give?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-037.json
---

## Question

A timed scenario ends with the resolution of the turn numbered with its limit
(RULE-OBJECTIVE-004), after the end evaluation has stored the scores
(RULE-OBJECTIVE-001). The endgame then gives the awards (RULE-AWARDS-001).
Every earlier run stops before a match ends. Does the original end a
six-month Greed with the 26th resolution, and which awards does it give?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-028 with 26 Done presses (`--end-turns 26`). After the last one
the end evaluation sets `match_over`, and the match loop gives the human one
last look at the city with the turn's Combat Results open (FND-OBJECTIVE-004).
The probe closes the panel, presses Done there, and stops when the endgame's
row painter at `0x0042CE61` first runs, once the awards are given.

## Observations

The run made 10794 calls of `roll`; the first 10081 are those of EXP-TURN-028,
which stops before its 26th Done. At the end `match_over` was 1 and `elapsed_turns` still 25, since the
loop moves it on only after the endgame. The stored scores were each player's
cash, with the human last. The human, who spent nothing, held award 4 (Safe),
player 1, with the most cash spent (372), held award 3 (Dollar Sign), and no
other player held one. Nobody overthrew, damaged or hid enough for the first
three awards.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results, ends the match with the resolution of turn 26, reaches
the same state, stored scores and Last Turn reports, and gives the same awards
to the same players.

## Conclusion

The run agrees with RULE-OBJECTIVE-001, RULE-OBJECTIVE-004 and
RULE-AWARDS-001.
