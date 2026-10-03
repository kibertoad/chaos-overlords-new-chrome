---
id: EXP-TURN-038
title: How does a six-month Dominance end, and does a site completed in the last turn count?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 2
fixture: EXP-TURN-038.json
---

## Question

Dominance weighs cash, sectors and the Support of the completed sites in a
player's sectors (RULE-OBJECTIVE-002). A site completed in a turn normally
counts from the rebuild before the next planning (RULE-SITE-001), and the
last turn has no next planning. Does the original count a site completed in
the final turn, in the scores and in the sectors it leaves?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-037 with `--seed 5` and `--scenario 3` (Dominance).
Before the first Done press the human's gang in roster slot 0 is set to Hide
with Hide as its recurring order (`--orders 1:0:8:0:0:1`), so it hides in
every turn.

## Observations

The run made 13333 calls of `roll`. At the end `match_over` was 1 and
`elapsed_turns` 25. The stored scores were 7, 91, 105, 107, 107 and 87 for
players 0 to 5. In sectors 21 and 38 a site was completed during turn 26, and
the sectors' Support and Tolerance at the end count it. The human held award
2 (Big Fat Chicken) and award 4 (Safe), player 3 held award 3 (Dollar Sign),
and no other player held one.

A second run with the same inputs, recorded once the probe kept the players
of the endgame's rows (FND-AWARDS-005), made the same calls of `roll` with
the same results and reached the same end state. The endgame's first drawing
was the table, with every player in a ranked row, in the order 3, 4, 2, 1, 5 and 0:
players 3 and 4, tied at standing 0, are listed in slot order and no player is listed at standing 1. The fixture holds this run.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, reaches the
same state, stored scores and Last Turn reports, and gives the same awards to
the same players. The scores count a site from the evaluation of the turn
that completes it, and the sectors count it after the rebuild that ends the
match.

The rebuild's endgame lists the players in the same order
(`TheEndgameListsThePlayersInTheOriginalsOrder`).

## Conclusion

The run agrees with RULE-OBJECTIVE-002, RULE-OBJECTIVE-004, RULE-AWARDS-001
and the rebuild of RULE-SITE-001 at the end of a match, and with RULE-AWARDS-002
for a table with a tie and a skipped standing.
