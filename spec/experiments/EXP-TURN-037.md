---
id: EXP-TURN-037
title: How does a six-month Acceptance end, and does a human that always hides get the Big Fat Chicken?
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

EXP-TURN-036 ends a Greed. Acceptance scores the Support of the completed
sites in a player's sectors (RULE-OBJECTIVE-002), and the match ends with one
more refresh of the sectors and gangs (FND-OBJECTIVE-004). Does the original
end a six-month Acceptance with the 26th resolution, and does a human that
hid every turn get award 2 (Big Fat Chicken) from RULE-AWARDS-001?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-036 with `--seed 1` and `--scenario 2` (Acceptance).
Before the first Done press the human's gang in roster slot 0 is set to Hide
with Hide as its recurring order (`--orders 1:0:8:0:0:1`), so it hides in
every turn.

## Observations

The run made 13650 calls of `roll`. At the end `match_over` was 1 and
`elapsed_turns` 25. The stored scores were 0, 16, 25, 21, 26 and 25 for
players 0 to 5. The human held award 2 (Big Fat Chicken) and award 4 (Safe),
player 2 held award 3 (Dollar Sign), and no other player held one.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, ends the match
with the resolution of turn 26, reaches the same state, stored scores and Last
Turn reports, and gives the same awards to the same players. The computer
players' last planning reads the gangs' statistics as the refresh at the end
of the match leaves them; without that refresh the rebuild's rolls part from
the original's in the hire-offer scan of the final turn.

## Conclusion

The run agrees with RULE-OBJECTIVE-002, RULE-OBJECTIVE-004, RULE-AWARDS-001
and the refresh of RULE-GANG-001 at the end of a match.
