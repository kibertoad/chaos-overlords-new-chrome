---
id: EXP-UI-010
title: Do the Give, Sell and Influence panels look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-010.json
---

## Question

On the detailed sector screen of sector 51 at the planning entry of the match
of EXP-TURN-031, where the human's second gang carries two items and the
sector holds a site the human controls completely, do the Give and Sell panels
of the second gang card and the Influence panel of the first draw the same
pixels in the rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-031, with `--white-key`, and after the dump `--order-steps
exit,exit,open:51,card:1:20:12:6,shot:SCR-GIVE-001+SCR-UI-004,strip:161:272:0,card:1:20:12:12,shot:SCR-SELL-001+SCR-UI-004,strip:161:272:0,card:0:20:12:9,shot:SCR-INFLUENCE-001+SCR-UI-004,strip:161:272:0`.
The `card` steps open menu 1 of card 1 with choice 6 (Give) and 12 (Sell),
and of card 0 with choice 9 (Influence), as in EXP-UI-009, and
`strip:161:272:0` presses the panels' Cancel face.

The shots are taken and their counters read as in EXP-UI-009. While the Sell
or Give handler runs, the probe reads its frame local, `[ebp-0x28]` or
`[ebp-0x2c]` (FND-UI-053), from the frame pointer it records when the handler
starts, as it does for Item Information.

## Observations

The run made the same 1908 calls of `roll` with the same bounds and results
as EXP-TURN-031. Menu 1 of card 1 had Attack and Control greyed; menu 1 of
card 0 had Attack, Control, Give, Heal and Sell greyed. All three shots were
kept, with sector 51 selected and every light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter | Item frame |
|---|---|---|---|---|---|
| 4 | SCR-GIVE-001, SCR-UI-004 | 3 | 4 | 7 | 5 |
| 7 | SCR-SELL-001, SCR-UI-004 | 8 | 6 | 1 | 5 |
| 10 | SCR-INFLUENCE-001, SCR-UI-004 | 0 | 0 | 3 | |

The captures show:

- the Give panel's recipient card with the recipient's portrait at half size,
  sampled as the Gangs in Sector panel samples its portraits;
- the Sell panel's item names and prices over the placeholder digits of the
  panel art, which the opaque glyph cells of the text and number routines
  cover (FND-UI-019), and an empty slot filled with black;
- the item pictures of Give and Sell at the recorded frame;
- the Influence panel's completed site through the dense pattern on black
  under the frame at `(362,299)` of `PX00129`, and the other sites under the
  frame at `(242,299)` (FND-INFLUENCE-002, FND-INFLUENCE-005).

## Results

A test of the rebuild compares the captures as for EXP-UI-009, drawing the
recorded item frame. Leaving out the cash row (DEV-UI-006) and the Tolerance
value (DEV-UI-007), every element of the three captures matches.

## Conclusion

The run supports SCR-GIVE-001, SCR-SELL-001 and SCR-INFLUENCE-001 for one
state each, FND-UI-053 for the item frame of Give and Sell, and
FND-INFLUENCE-005 for the pattern of a completed site.
