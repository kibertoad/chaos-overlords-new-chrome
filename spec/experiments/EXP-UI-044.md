---
id: EXP-UI-044
title: Does the Comlink Send panel look the same in the rebuild while Send is held, and does its caret stop for the hold?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-044.json
---

## Question

In the match of two local humans of EXP-UI-016, with player 1's card chosen
on the Comlink Send panel, what does the original draw while Send is held
under the pointer, while it is held with the pointer moved off it, and after
the button comes up off it? Does the caret take no tick of timer slot 0 while
Send is held?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --humans 0,1 --seed 7 --end-turns 0
--white-key --order-steps
strip:320:265:0,strip:576:166:0,strip:250:190:0,shot:SCR-COMLINK-002+SCR-UI-003,down:161:304,shot:SCR-COMLINK-002+SCR-UI-003,move:300:200,shot:SCR-COMLINK-002+SCR-UI-003,wait:1000,up:300:200,wait:500,shot:SCR-COMLINK-002+SCR-UI-003,strip:161:272:0,shot:SCR-UI-003`.

The steps press the hand-off card's Ready, the lower half of the Comlink
control and player 1's card as in EXP-UI-016, then hold Send, move the pointer
off it, wait, release off it, wait and press Cancel. The button steps are
those of EXP-UI-041, the probe keeps the slot 0 clears per step as there, and
it keeps the caret phase as the shot's `caret_phase` as in EXP-UI-016.

## Observations

The run made 307 calls of `roll` before the dump and three more after Ready,
the same 310 calls as EXP-UI-016. All five shots were kept, with sector 33
selected and every light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter | Caret phase |
|---|---|---|---|---|---|
| 3 | SCR-COMLINK-002, SCR-UI-003 | 1 | 0 | 6 | 3 |
| 5 | SCR-COMLINK-002, SCR-UI-003 | 1 | 0 | 6 | 3 |
| 7 | SCR-COMLINK-002, SCR-UI-003 | 1 | 0 | 6 | 3 |
| 11 | SCR-COMLINK-002, SCR-UI-003 | 3 | 1 | 6 | 0 |
| 13 | SCR-UI-003 | 0 | 5 | 5 | |

The shot of step 5 shows the lit Send face of FND-UI-062 under the pointer.
The shot of step 7, with the pointer off the held face, has the same pixels as
the shot of step 3 before the press: the face the original puts back equals
the Send face a chosen recipient leaves. After the release off it, Send keeps
that face and the panel stays open with the caret plain.

Send's loop cleared slot 0 about every 170 milliseconds before the press. From
the press to the release, steps 4 to 8, no call cleared it, for more than a
second, and the marker frame, pump counter and caret phase stayed the same
across the three shots of the hold. The release step recorded clears at 0,
51, 204, 374, 546 and 716 milliseconds.

## Results

A test of the rebuild compares the captures as for EXP-UI-041, drawing the
recorded item frame, and every element of the five captures matches.

## Conclusion

The run supports SCR-COMLINK-002 with Send held, moved off and released off,
FND-UI-062 for the face a held Send leaves once the pointer is off it, and
FND-UI-047 for the Send caret: its loop takes no tick while the face is held
and takes the kept one in the pass of the release.
