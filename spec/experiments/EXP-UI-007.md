---
id: EXP-UI-007
title: Do the detailed sector screen, Site Information, Gangs in Sector and the Sector Financial panel look the same in the rebuild for a sector under police presence?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-007.json
---

## Question

For a neutral sector holding the human's three gangs and police presence,
do the detailed sector screen, the Site Information panel of one of its sites,
the Gangs in Sector panel and the Sector variant of the Financial panel draw
the same pixels in the rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071 (`--scenario 0 --mentality 1 --end-turns 6 --seed 16`, with
its hires and orders), with `--white-key`, and after the dump `--order-steps
exit,exit,open:19,shot:SCR-UI-004,dbl:146:260,shot:SCR-UI-007,strip:185:304:0,back,strip:524:246:0,shot:SCR-UI-005,strip:161:304:0,strip:576:218:0,shot:SCR-FINANCE-001,strip:185:304:0`.
The shots are taken as in EXP-UI-006. A `dbl` step double-clicks a window
point as `open` does a sector's centre; here it opens the site portrait of
slot 0 on the detailed sector screen (SCR-UI-004). The other steps close the
panels the planning entry opened, double-click sector 19, press Site
Information's Exit, the back control, Gangs in Sector and its Exit, the
console's Sector Financial control and the panel's Exit. With each shot the
probe also reads the Events light's flag `0x00487814` and the byte
`0x00487818` the pump sets when it draws that lamp, and the Comlink light's
`0x0048781C` and `0x00487820` (FND-EVENT-006), and the selected sector and
frame counter as in EXP-UI-008.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071. Both Exit steps closed a panel: Combat Results, then the Last
Turn Events panel the planning entry opens after it (RULE-EVENT-005). All
four shots were kept:

| Step | Screens | Marker frame | Pump counter | Frame counter | Light bytes |
|---|---|---|---|---|---|
| 3 | SCR-UI-004 | 8 | 6 | 6 | 0, 0, 0, 0 |
| 5 | SCR-UI-007 | 4 | 3 | 6 | 0, 0, 0, 0 |
| 9 | SCR-UI-005 | 4 | 2 | 5 | 0, 0, 0, 0 |
| 12 | SCR-FINANCE-001 | 9 | 4 | 7 | 0, 0, 0, 0 |

Sector 19 was selected in every shot. The frame counter is the one EXP-UI-008
describes; the three panels hold the frame of the counter at their slide-in
(FND-UI-051).

The captures show:

- in the nine-sector display, a badge over the centre cell, sector 19, whose
  `crackdown_turns` is 3 (FND-UI-050);
- the Site Information picture under a keyed frame, and the site's special
  line under the Cash row (FND-UI-049);
- the Sector Financial panel's portrait box holding the sector's cell of the
  unmarked city map with a black border, where the City variant holds the
  Overlord's portrait;
- the Events light dark, with its flag clear, after the Last Turn Events panel
  was closed.

## Results

A test of the rebuild compares the captures as for EXP-UI-006. The rebuild's
reference frame closes Last Turn Events as an Exit press after the first page
does, so the light stays lit only when another report is unseen; here the
human's one report is the Crackdown in sector 19. Leaving out the cash row
(DEV-UI-006) and the Tolerance value (DEV-UI-007), every element of the four
captures matches.

## Conclusion

The run supports SCR-UI-004, SCR-UI-005, SCR-UI-007 and SCR-FINANCE-001
(Sector variant) for one state with police presence, FND-UI-049 and
FND-UI-050, and RULE-EVENT-005 for the light after the panel shows its only
report.
