---
id: EXP-UI-032
title: What does the elimination card show behind it after an earlier human has planned?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-032.json
---

## Question

In a match of two local humans, when the second human was eliminated in the
last turn, its card comes after the first human has planned the next turn
(RULE-OBJECTIVE-005). Is the screen behind that card black, or the first
human's city as it was left? Does the card draw the same pixels in the rebuild?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 7 --humans 0,1 --seed 4210
--end-turns 1 --deactivate 1:1:0 --white-key --order-steps
strip:320:265:0,exit,exit,strip:550:306:0,shot:SCR-SETUP-002,strip:320:265:0,wait:1500,shot:SCR-OBJECTIVE-002`.

The match is Eliminate, where a player whose Right Hands (roster slot 0) are
gone loses everything at the end of the turn (RULE-TURN-006). In turn 1 the
probe presses Ready on player 0's hand-off card, writes sector 100, the value
of a gang that is gone (FMT-STATE-001), into the sector byte of player 1's
roster slot 0, presses Done, and then presses Ready and Done with no orders
for player 1. The resolution eliminates player 1. The state is dumped at
player 0's hand-off card of turn 2.

After the dump the steps press Ready on that card, Exit on each panel the
planning entry opens, and Done; copy the screen, which then shows player 1's
hand-off card; press its Ready; wait 1.5 seconds; and copy the screen again.

## Observations

The run made 388 calls of `roll` before the dump, the turn's Done after call
302. The two copies were kept with selected sector 12, marker frame 2 and
every light byte 0:

| Step | Screens | Shows |
|---|---|---|
| 4 | SCR-SETUP-002 | Player 1's hand-off card on black |
| 7 | SCR-OBJECTIVE-002 | Player 1's elimination card on black |

The hand-off card blanks the screen around itself. The elimination card's
frame and splash cover the card, so nothing of player 0's city is left behind
it. The splash's colour band, the three areas `(110, 30, 40, 12)`,
`(110, 42, 13, 79)` and `(110, 121, 40, 302)` that SCR-AWARDS-002 fills on the
victory splash, is in player 1's green.

## Results

A test of the rebuild replays the 388 rolls, with the Right Hands taken out of
the match as the write does, and Ready and Done for player 1. Another test
presses the same controls from the replayed endpoint and compares both copies.
Every element matches.

A first comparison found the rebuild filling the colour band only on the
victory splash, so the card of player 1 showed the band of the splash's art.

## Conclusion

The run supports SCR-OBJECTIVE-002 and RULE-OBJECTIVE-005 for the card of an
eliminated human that follows another human's planning: it comes after that
human's hand-off card, on black, with its colour band in the eliminated
player's colour.
