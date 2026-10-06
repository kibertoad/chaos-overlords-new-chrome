---
id: EXP-UI-041
title: Do the held faces, a dragged hire offer and the chosen rows of the order panels look the same in the rebuild, and do the panels stop their ticks while a face is held?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-041.json
---

## Question

At the planning entry of turn 7 of the match of EXP-TURN-071, with the left
button held down across a capture, do the city console's tiles, the close
faces of the information panels, the sector view's back control and the
order panels' faces draw the same pixels in the rebuild as in the original,
while the pointer is over the held control, after it has moved off, and after
a release off it? Do a hire offer dragged over the city map, the Move panel
with a destination, the Equip panel with a row chosen and the Research panel
with a row chosen look the same? And does Item Information take no tick of
timer slot 0 while its face is held?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071, with `--white-key`, and after the dump `--order-steps
exit,exit,down:576:240,shot:SCR-UI-003,move:300:300,shot:SCR-UI-003,up:300:300,strip:576:240:0,down:161:304,shot:SCR-OBJECTIVE-001+SCR-UI-003,move:300:200,shot:SCR-OBJECTIVE-001+SCR-UI-003,up:300:200,shot:SCR-OBJECTIVE-001+SCR-UI-003,strip:161:304:0,strip:524:150:0,down:161:304,shot:SCR-EVENT-001+SCR-UI-003,up:161:304,strip:524:190:0,down:161:304,shot:SCR-COMBAT-001+SCR-UI-003,up:161:304,strip:576:266:0,down:161:150,shot:SCR-SEARCH-001+SCR-UI-003,up:300:250,shot:SCR-SEARCH-001+SCR-UI-003,strip:161:304:0,strip:524:270:0,down:185:304,shot:SCR-HIRE-001+SCR-UI-003,up:185:304,dbl:472:405,down:161:304,shot:SCR-GANG-002+SCR-UI-003,up:161:304,down:472:405,move:380:300,move:300:250,shot:SCR-UI-003+SCR-HIRE-002,move:300:20,up:300:20,open:19,down:20:425,shot:SCR-UI-004,move:300:300,shot:SCR-UI-004,up:300:300,card:0:20:12:10,strip:263:176:0,shot:SCR-MOVE-001+SCR-UI-004,down:161:272,shot:SCR-MOVE-001+SCR-UI-004,up:161:272,card:0:20:12:5,strip:300:154:0,wait:1000,shot:SCR-EQUIP-001+SCR-UI-004,down:161:304,shot:SCR-EQUIP-001+SCR-UI-004,up:300:200,shot:SCR-EQUIP-001+SCR-UI-004,dbl:300:154,shot:SCR-UI-006+SCR-UI-004,down:185:304,wait:2000,shot:SCR-UI-006+SCR-UI-004,move:300:250,shot:SCR-UI-006+SCR-UI-004,wait:1000,up:300:250,wait:1000,shot:SCR-UI-006+SCR-UI-004,strip:185:304:0,strip:161:272:0,card:0:20:12:11,strip:304:154:0,shot:SCR-RESEARCH-001+SCR-UI-004,strip:161:272:0`.

`down:x:y` writes both pointer points of FND-UI-020 and posts only
`WM_LBUTTONDOWN`, `move:x:y` writes the points with the button still down, and
`up:x:y` writes them and posts `WM_LBUTTONUP`; a shot taken while the button
is down writes the held point again and pumps 0.3 seconds before it copies
(docs/VALIDATION.md). The steps hold the Ranking tile and move off it; hold
Player Rankings' OK, move off and release off it; hold the Exit of Last Turn
Events, Combat Results, the Hire comparison and the offer's gang information;
hold Search's ALL and release off it; drag the first offer over the city map
and drop it on the Overlord bar, where the drop changes nothing; hold the
sector view's back control and move off it; give Move a destination and hold
its Cancel; choose Equip's first row, hold its confirm face and release off
it; and hold Item Information's Exit for two seconds, move off it, wait a
second, release and wait a second. Every other step is as in EXP-UI-008 and
EXP-UI-009. The probe keeps, for each step, the milliseconds from its start to
each call that clears timer slot 0 in the panel loops of FND-UI-047.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071. Each `card` step opened menu 1. All 24
shots were kept, with every light byte 0, sector 12 selected on the city
screen and sector 19 on the sector view:

| Step | Screens | Marker frame | Pump counter | Frame counter | Item frame |
|---|---|---|---|---|---|
| 3 | SCR-UI-003 | 5 | 0 | 0 | |
| 5 | SCR-UI-003 | 5 | 0 | 0 | |
| 9 | SCR-OBJECTIVE-001, SCR-UI-003 | 10 | 3 | 6 | |
| 11 | SCR-OBJECTIVE-001, SCR-UI-003 | 10 | 3 | 6 | |
| 13 | SCR-OBJECTIVE-001, SCR-UI-003 | 6 | 1 | 6 | |
| 17 | SCR-EVENT-001, SCR-UI-003 | 11 | 3 | 6 | |
| 21 | SCR-COMBAT-001, SCR-UI-003 | 4 | 6 | 1 | |
| 25 | SCR-SEARCH-001, SCR-UI-003 | 9 | 1 | 4 | |
| 27 | SCR-SEARCH-001, SCR-UI-003 | 6 | 7 | 4 | |
| 31 | SCR-HIRE-001, SCR-UI-003 | 0 | 2 | 5 | |
| 35 | SCR-GANG-002, SCR-UI-003 | 5 | 5 | 0 | |
| 40 | SCR-UI-003, SCR-HIRE-002 | 2 | 2 | 2 | |
| 45 | SCR-UI-004 | 7 | 5 | 5 | |
| 47 | SCR-UI-004 | 7 | 5 | 5 | |
| 51 | SCR-MOVE-001, SCR-UI-004 | 8 | 5 | 3 | |
| 53 | SCR-MOVE-001, SCR-UI-004 | 9 | 5 | 3 | |
| 58 | SCR-EQUIP-001, SCR-UI-004 | 9 | 3 | 3 | |
| 60 | SCR-EQUIP-001, SCR-UI-004 | 9 | 3 | 3 | |
| 62 | SCR-EQUIP-001, SCR-UI-004 | 6 | 1 | 3 | |
| 64 | SCR-UI-006, SCR-UI-004 | 3 | 6 | 3 | 5 |
| 67 | SCR-UI-006, SCR-UI-004 | 3 | 6 | 3 | 5 |
| 69 | SCR-UI-006, SCR-UI-004 | 3 | 6 | 3 | 5 |
| 73 | SCR-UI-006, SCR-UI-004 | 11 | 1 | 3 | 2 |
| 78 | SCR-RESEARCH-001, SCR-UI-004 | 7 | 4 | 3 | |

The captures show:

- the Ranking tile drawn pressed while it is held under the pointer and drawn
  as before the press once the pointer has left it with the button still down;
  the sector view's back control the same way;
- each held close, Exit, ALL, Cancel and confirm face drawn with its lit face
  while the pointer is over it, and with the plain face of `PX00129` once the
  pointer has left it and after a release off it, with the panel still open;
- the dragged offer as a 40-by-40 image centred on the pointer: the portrait
  shrunk, inside a black outer and a grey inner one-pixel frame, with no mark
  on the sector under it (FND-HIRE-010);
- the chosen Equip row's strip ending in the item's price in the strip's font
  (FND-EQUIP-011).

While Item Information's Exit was held, from step 65 to step 70, no call
cleared timer slot 0, and the item frame stayed at 5 across the two shots and
more than three seconds. The release step recorded clears at 0, 170, 340, 510,
663 and 833 milliseconds, and the next step's wait at intervals of about 170
milliseconds; the shot after them shows item frame 2.

The marker and pump counters stood still across the shots of a hold (steps 9
and 11, 45 and 47, and 64 to 69, the first of those taken just before the
press): the event pump did not run while the button was held.

An earlier run of the same steps without the wait after the Equip row
captured the Equip panel before the row was drawn chosen, and two of its
shots showed the Overlord bar's marker area black; those recordings are not
kept.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` replays the steps as reference
clicks, each `down`, `move` and `up` as an edge of its own, and compares the
captures as for EXP-UI-009. Leaving out the cash row (DEV-UI-006), the
Tolerance value (DEV-UI-007), the key line (DEV-UI-023) and the Research
panel's progress column (DEV-RESEARCH-001), every element of the 24 captures
matches.

## Conclusion

The run supports FND-UI-044 and FND-UI-062 for the console tile, the sector
view's back control and the faces held through `fn_00418821`, FND-UI-067 for
the information panels' close faces, FND-HIRE-010 for the dragged offer,
FND-EQUIP-011 for the chosen Equip row, and FND-UI-047 for Item Information:
its loop takes no tick while the face is held and takes the kept one in the
pass of the release.
