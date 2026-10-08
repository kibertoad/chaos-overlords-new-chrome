---
id: EXP-UI-042
title: Do the Give, Sell and Influence panels look the same in the rebuild with a choice made and a face held, and does Sell stop its ticks while the face is held?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-042.json
---

## Question

On the detailed sector screen of sector 51 after the inputs of EXP-TURN-031,
do the Give panel with an item and a recipient chosen, the Sell panel with an
item chosen and its Cancel held, and the Influence panel with a site chosen
draw the same pixels in the rebuild as in the original? Does Sell take no tick
of timer slot 0 while its Cancel is held?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-031, with `--white-key`, and after the dump `--order-steps
exit,exit,open:51,card:1:20:12:6,strip:233:229:0,strip:360:156:0,shot:SCR-GIVE-001+SCR-UI-004,strip:161:272:0,card:1:20:12:12,strip:310:229:0,shot:SCR-SELL-001+SCR-UI-004,down:161:272,shot:SCR-SELL-001+SCR-UI-004,move:300:200,shot:SCR-SELL-001+SCR-UI-004,up:300:200,strip:161:272:0,card:0:20:12:9,strip:270:173:0,shot:SCR-INFLUENCE-001+SCR-UI-004,strip:161:272:0`.
`strip:233:229:0` presses the Give panel's first item, `strip:360:156:0` its
first recipient, `strip:310:229:0` the Sell panel's first item and
`strip:270:173:0` the Influence panel's first site. The button steps are those
of EXP-UI-041, and the probe keeps the slot 0 clears per step as there.

## Observations

The run made 1586 calls of `roll`. It made the same calls as EXP-TURN-031 up
to call 506, in turn 3; call 507 was the one at `0x0047172A` with bound 89
where EXP-TURN-031 made another call at `0x00475AC6` with bound 5, and from
there the match is this run's own. What made the two runs part was not found.
The human's second gang still carried two items and the gang was in sector 51
with the first. Menu 1 of card 1 had Attack and Control greyed; menu 1 of
card 0 had Attack, Control, Give, Heal and Sell greyed. All five shots were
kept, with sector 51 selected and every light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter | Item frame |
|---|---|---|---|---|---|
| 6 | SCR-GIVE-001, SCR-UI-004 | 8 | 7 | 0 | 14 |
| 10 | SCR-SELL-001, SCR-UI-004 | 9 | 6 | 4 | 9 |
| 12 | SCR-SELL-001, SCR-UI-004 | 9 | 6 | 4 | 10 |
| 14 | SCR-SELL-001, SCR-UI-004 | 9 | 6 | 4 | 10 |
| 19 | SCR-INFLUENCE-001, SCR-UI-004 | 7 | 2 | 0 | |

The captures show the Give panel's chosen item and recipient, the Sell
panel's chosen item, the lit Cancel face while it is held under the pointer
and, once the pointer has left it, the plain Cancel face of `PX00129`, whose
frame and text are one shade darker than the panel art's, and the Influence
panel's chosen site.

Sell's loop cleared slot 0 about every 170 milliseconds before the press. From
the press to the release, steps 11 to 14, no call cleared it and the item
frame stayed at 10 across both shots of the hold. The release step recorded
clears at 0, 68, 239, 391, 562 and 733 milliseconds.

## Results

A test of the rebuild compares the captures as for EXP-UI-041, drawing the
recorded item frame. Leaving out the cash row (DEV-UI-006) and the Tolerance
value (DEV-UI-007), every element of the five captures matches.

## Conclusion

The run supports SCR-GIVE-001, SCR-SELL-001 and SCR-INFLUENCE-001 with a
choice made, FND-UI-062 for the plain face a held Cancel leaves once the
pointer is off it, and FND-UI-047 for Sell: its loop takes no tick while the
face is held and takes the kept one in the pass of the release.
