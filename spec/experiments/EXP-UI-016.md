---
id: EXP-UI-016
title: Do the hand-off card and the Comlink Send panel look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory until the steps after the dump, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-016.json
---

## Question

In a match of two local humans, do the hand-off card that the first planning
entry opens, and the Comlink Send panel opened from the console, draw the same
pixels in the rebuild as in the original? What do the two halves of the
console's Comlink control do there?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --humans 0,1 --seed 7 --end-turns 0
--white-key --order-steps
shot:SCR-SETUP-002,strip:320:265:0,strip:576:140:0,shot:SCR-UI-003,strip:576:166:0,shot:SCR-COMLINK-002+SCR-UI-003,strip:250:190:0,shot:SCR-COMLINK-002+SCR-UI-003,strip:161:272:0,shot:SCR-UI-003`.

The state is dumped at the first planning entry of player 0, which with two
humans waits on the hand-off card. The steps copy the card, press its Ready,
press the upper and lower halves of the Comlink control (RULE-UI-002), press
the card of player 1 on the Send panel and then Cancel, with a copy after each
of the last three presses. For a copy of the Send panel the probe reads the
caret's phase byte `0x00498110` before and after the copy (FND-COMLINK-010)
and keeps it as the shot's item frame, 3 while it is 0 and 0 while it is set,
when the two reads agree.

## Observations

The run made 307 calls of `roll` before the dump and three more after Ready,
the offer draws of `0x0047172A` (FND-RNG-006). The shots were kept:

| Step | Screens | Item frame | Shows |
|---|---|---|---|
| 0 | SCR-SETUP-002 | None | The hand-off card of player 0 on black |
| 3 | SCR-UI-003 | None | The city, after the upper Comlink half with an empty inbox opened nothing |
| 5 | SCR-COMLINK-002, SCR-UI-003 | 3 | The Send panel with no recipient selected and the caret inverse |
| 7 | SCR-COMLINK-002, SCR-UI-003 | 3 | Player 1's card framed in green and the Send face changed |
| 9 | SCR-UI-003 | None | The city after Cancel |

On the Send panel player 1's card has its full colour, portrait and bright
name, and the cards of player 0 and the four computer players have dimmed bars,
other portraits and dim names. Between steps 5 and 7 the Send face at
`(137, 293)` changed as well as the card's frame.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the captures with the
presses before each replayed as reference clicks, the planning entry opening
the hand-off card, and the Send panel's caret drawn in the recorded phase.
Leaving out the cash row (DEV-UI-006) and the city's key line (DEV-UI-023),
every element matches. `TheRebuildStartsTheSameMatch` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the 307 rolls
before the dump.

A first comparison found the rebuild's hand-off card 18 pixels lower, with a
centred name, no colour bar and a larger portrait; its Send cards filled in
green when selected, with grey bars and full portraits for the slots that
cannot be sent to and the names one pixel up and left; no Send face after the
card press; and the caret plain.

## Conclusion

The run supports SCR-SETUP-002 for the card of the first planning entry,
SCR-COMLINK-002 for the panel as it opens and after a press on an eligible
card, FND-COMLINK-007 for the cards, FND-UI-019 for the enabled Send face
drawn once a recipient is selected, and RULE-UI-002 for both halves of the
Comlink control.
