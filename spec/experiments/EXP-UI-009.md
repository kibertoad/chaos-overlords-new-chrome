---
id: EXP-UI-009
title: Do the Move, Equip and Research panels, and the Item Information and compact Gang Information panels Equip opens, look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-009.json
---

## Question

On the detailed sector screen of sector 19 at the planning entry of turn 7 of
the match of EXP-TURN-071, do the Move, Equip and Research panels of the first
gang card, the Item Information panel of the first Equip row and the compact
Gang Information panel of the Equip portrait draw the same pixels in the
rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071, with `--white-key`, and after the dump `--order-steps
exit,exit,open:19,card:0:20:12:10,shot:SCR-MOVE-001+SCR-UI-004,strip:161:272:0,card:0:20:12:5,shot:SCR-EQUIP-001+SCR-UI-004,dbl:300:154,shot:SCR-UI-006+SCR-UI-004,strip:185:304:0,dbl:162:173,shot:SCR-GANG-001+SCR-UI-004,strip:185:304:0,strip:161:272:0,card:0:20:12:11,shot:SCR-RESEARCH-001+SCR-UI-004,strip:161:272:0`.
A `card` step presses card 0 at `(20, 12)` within the card, which opens menu 1
(FND-UI-021); the probe skips `TrackPopupMenu` and hands the helper the
step's command as the choice: 10 for Move, 5 for Equip and 11 for Research.
`strip:161:272:0` presses the panels' Cancel face, `dbl:300:154` double-clicks
the first Equip row, `dbl:162:173` the Equip portrait, and `strip:185:304:0`
the Exit face of the panel on top.

The shots are taken and their counters read as in EXP-UI-008. While Item
Information's handler runs, the probe also reads its frame local
`[ebp-0x134]` (FND-UI-052) before and after each capture, from the frame
pointer it records at `0x0044B6AC`, and keeps it as the shot's item frame when
the two reads agree.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071. Each `card` step opened menu 1 with Attack, Control, Give,
Heal, Influence and Sell greyed. All five shots were kept, with sector 19
selected and every light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter | Item frame |
|---|---|---|---|---|---|
| 4 | SCR-MOVE-001, SCR-UI-004 | 3 | 2 | 5 | |
| 7 | SCR-EQUIP-001, SCR-UI-004 | 8 | 4 | 7 | |
| 9 | SCR-UI-006, SCR-UI-004 | 4 | 1 | 7 | 5 |
| 12 | SCR-GANG-001, SCR-UI-004 | 8 | 3 | 7 | |
| 16 | SCR-RESEARCH-001, SCR-UI-004 | 9 | 2 | 5 | |

The captures show:

- the Research panel opened with no row selected for a gang whose order is
  not Research, its list on a black area, and each row's
  `research_remaining`;
- the Item Information panel over the Equip panel with the selection frame
  of the counter at the Equip panel's slide-in: Item Information's own
  slide-in found the byte of FND-UI-051 set, and the shot of the compact
  Gang Information panel, opened after Item Information slid out with its
  second argument set, shows the same frame;
- the item type in Item Information as string 25 plus the item's type
  (FND-UI-013), and the rotating item at the recorded frame.

A first run of the same steps recorded the item frame from a breakpoint at
`0x0044C3B4` and the counter at every slide-in; the item frame came out one
ahead of the picture and the Item Information shot's frame counter was the one
at its own slide-in. Those recordings are not kept.

## Results

A test of the rebuild compares the captures as for EXP-UI-008, drawing the
recorded item frame. The rebuild's orders are a panel (DEV-UI-021), so the test
replays a menu 1 choice that runs a picker as the card press and a press on that
order's row of the rebuild's panel. Leaving out the cash row (DEV-UI-006), the
Tolerance value (DEV-UI-007) and the Research panel's progress column
(DEV-RESEARCH-001), every element of the five captures matches.

## Conclusion

The run supports SCR-MOVE-001, SCR-EQUIP-001, SCR-RESEARCH-001, SCR-UI-006
and SCR-GANG-001 for one state each, FND-UI-051 for a panel that slides in and
out over another, and FND-UI-052 for the item frame.
