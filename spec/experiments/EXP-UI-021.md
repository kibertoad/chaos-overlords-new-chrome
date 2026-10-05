---
id: EXP-UI-021
title: Does the Comlink View panel look the same in the rebuild for a message one human sent another?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory until the steps after the dump, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-021.json
---

## Question

In a local match of two humans, after player 0 sends player 1 a message and
ends its planning, does Comlink View draw the same pixels in the rebuild as in
the original when player 1 opens it?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --humans 0,1 --seed 7 --end-turns 0
--white-key --order-steps
strip:320:265:0,strip:576:166:0,type:MEET ME IN B2 AT DAWN 0700,strip:250:190:0,strip:162:304:0,strip:550:306:0,strip:161:304:0,shot:SCR-SETUP-002,strip:320:265:0,strip:576:140:0,shot:SCR-COMLINK-001+SCR-UI-003`.

The state is dumped at player 0's first planning entry, on the hand-off card,
as in EXP-UI-016. The steps press Ready, open Comlink Send, type the message
with a key press for each character as the Comlink script does (FND-UI-020),
select player 1's card, press Send, press Done and then OK on the idle-gang
warning it opens, copy player 1's hand-off card, press its Ready, press the
upper half of the Comlink control and copy the screen. Each Ready begins a
planning entry, which draws that player's hire offers at `0x0047172A`
(FND-RNG-006); the probe lets those calls of `roll` through and stops on any
other.

## Observations

The run made 307 calls of `roll` before the dump and three hire offer draws
after each Ready, 313 in all. Both shots were kept with every light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter | Selected sector |
|---|---|---|---|---|---|
| 7 | SCR-SETUP-002 | 4 | 5 | 5 | 33 |
| 10 | SCR-COMLINK-001, SCR-UI-003 | 9 | 7 | 3 | 9 |

The View panel shows message 01 of 01, dated 2050.01, from player 0 with its
name, colour strip and portrait stretched to 64 by 64, and the typed text on
the first row.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` replays the same presses and the
typed text from the dumped state and compares the elements of SCR-SETUP-002,
SCR-COMLINK-001 and SCR-UI-003. Leaving out the cash row (DEV-UI-006) and the
key line (DEV-UI-023), no element differs.

## Conclusion

The run supports SCR-COMLINK-001 for one message, RULE-COMLINK-003 for a
message sent to one recipient, and the field positions of FND-COMLINK-007.
