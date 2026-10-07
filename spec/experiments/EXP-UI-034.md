---
id: EXP-UI-034
title: What does the endgame show behind it after an elimination card?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-034.json
---

## Question

In a match of two local humans that ends in the turn one of them is
eliminated, the endgame (SCR-AWARDS-001) comes after that human's elimination
card. Is the screen around the endgame's frame black, or does it keep what the
card or the city left? Does the endgame draw the same pixels in the rebuild?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 7 --humans 0,1 --seed 4190
--end-turns 2 --deactivate 1:1:0 --retire 1:2,1:3,1:4,1:5 --pass-cards
--white-key --order-steps
shot:SCR-AWARDS-002,strip:504:57:0,shot:SCR-AWARDS-001`.

The match is Eliminate. In turn 1 player 0 presses Ready, the probe writes
`player_active` 0 for the four computer players (FND-STATE-004) and sector 100
into the sector byte of player 1's roster slot 0, as in EXP-UI-032, and player
0 presses Done; player 1 presses Ready and Done with no orders. The resolution
eliminates player 1 and leaves player 0 the only active player, which ends the
match. With `--pass-cards` the probe presses Done on every elimination card it
reaches and goes on. The state is dumped at the awards.

After the dump the steps copy the Awards tab, which shows the victory splash
(SCR-AWARDS-002), press Stats and copy again. The fixture's inputs also list
the presses of turn 2, which the run did not reach.

## Observations

The run made 335 calls of `roll`, the turn's Done after call 332. After the
resolution player 0 saw its Ready card and its final view, with Last Turn
Events, and pressed Done; then player 1's Ready card and elimination card came,
and the probe passed the card with `active_player` 1. The awards came last,
with one row kind, the splash of player 0.

| Step | Screens | Shows |
|---|---|---|
| 0 | SCR-AWARDS-002 | Player 0's victory splash in the endgame frame, on black |
| 2 | SCR-AWARDS-001 | The Stats tab with the six players' rows, on black |

## Results

A test of the rebuild replays the 335 rolls, with the computer players retired
and player 1's Right Hands taken out as the writes do. Another test draws the
replayed endpoint, which stands at the awards, and compares both copies. Every
element matches.

A first comparison found the rebuild's reference frame showing player 1's
Ready card for the save of the decided match, where the original had shown
every card before the awards.

## Conclusion

The run supports SCR-AWARDS-001 and SCR-AWARDS-002 for an endgame that follows
an elimination card: the screen around the frame is black. It supports
RULE-OBJECTIVE-005 for the cards at the end of a match: the final view of the
surviving human and the eliminated human's Ready card and card come before the
awards.
