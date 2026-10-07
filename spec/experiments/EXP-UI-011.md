---
id: EXP-UI-011
title: Does the Attack picker look the same in the rebuild for a sector holding several opponents' gangs?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-011.json
---

## Question

In the state of EXP-ATTACK-003, where the human's one gang in sector 33 sees
six gangs of players 2, 4 and 5, does the Attack picker draw the same pixels
in the rebuild as in the original when it opens, after a press on the chosen
opponent, and after a target is chosen?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-UI-006 (`--scenario 9 --mentality 1 --seed 5490 --end-turns 28
--white-key`), with `--order-steps
exit,exit,exit,open:33,card:0:20:12:1,shot:SCR-ATTACK-001+SCR-UI-004,strip:218:192:0,shot:SCR-ATTACK-001+SCR-UI-004,strip:272:184:0,shot:SCR-ATTACK-001+SCR-UI-004,strip:161:272:0`.
The `card` step opens menu 1 of card 0 with choice 1 (Attack), as in
EXP-UI-009. `strip:218:192:0` presses opponent portrait 1, player 2's, and
`strip:272:184:0` target cell 0; the last step presses Cancel. The shots are
taken and their counters read as in EXP-UI-009.

## Observations

The run made the same 14892 calls of `roll` with the same bounds and results
as EXP-TURN-026. Menu 1 of card 0 greyed Give, Influence and Sell, and left
Research enabled. All three shots were kept, with sector 33 selected and every
light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter |
|---|---|---|---|---|
| 5 | SCR-ATTACK-001, SCR-UI-004 | 4 | 3 | 6 |
| 7 | SCR-ATTACK-001, SCR-UI-004 | 0 | 0 | 6 |
| 9 | SCR-ATTACK-001, SCR-UI-004 | 8 | 5 | 6 |

The captures show:

- the picker opened with player 2 chosen, the first opponent whose portrait
  is enabled, and its four gangs listed (FND-ATTACK-003, FND-ATTACK-004);
  steps 5 and 7 differ only in the Overlord bar's marker;
- in the target cards, the armor icons drawn 19 pixels wide with the icon's
  first 19 columns: the stretch of FND-ATTACK-007 leaves out the icon's last
  column;
- at step 9 the target marker over cell 0 and the confirm face enabled.

## Results

A test of the rebuild compares the captures as for EXP-UI-009. Leaving out the
cash row (DEV-UI-006) and the Tolerance value (DEV-UI-007), every element of the
three captures matches. Another test checks the menu against the rebuild's order
panel, which leaves Research available with nothing to research, as the
original's menu does (FND-UI-021).

## Conclusion

The run supports SCR-ATTACK-001 for one state with targets listed, before and
after a target is chosen, FND-ATTACK-007, and FND-UI-021 for a menu 1 that
leaves Research enabled when the gang has nothing to research.
