---
id: EXP-TURN-108
title: Does an Armageddon match at Goon end on the turn a computer player holds all 64 sectors?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-108.json
---

## Question

No recorded run ends an Armageddon match on its objective, and no other run
plays beyond turn 88. Does an Armageddon match at Goon, with the human hiding, end
on the turn the rebuild ends it, with the same scores and awards?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-107 with Armageddon (`--scenario 9`), `--seed 1` and up to 130
Done presses (`--end-turns 130`). The human's gang in roster slot 0 hides in
every turn (`--orders 1:0:8:0:0:1`). The seed was chosen by playing the same
settings in the rebuild, which ends the match after 124 turns with the human
still in it.

## Observations

The run made 113589 calls of `roll` over 124 Done presses. At the end
`match_over` is 1 and `elapsed_turns` 123. Player 3 holds a stored score of
64 and every other player 0, and every player is still active: the others
hold no sector but still have gangs. The endgame ranks players 3, 0, 1, 2, 4
and 5.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results, ends the match
after the same Done press, and reaches the same state, stored scores,
standings, endgame rows, Last Turn reports and awards.

## Conclusion

The run agrees with RULE-OBJECTIVE-001, RULE-OBJECTIVE-002 and
RULE-OBJECTIVE-004 for an Armageddon match ended on its objective, and with
RULE-AWARDS-001 and RULE-AWARDS-002, through 124 turns of the computer
players' planning.
