---
id: EXP-UI-008
title: Do the console's Events, Combat Results, Rankings, Search and Hire panels and the Gang Information panel of a hire offer look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-008.json
---

## Question

At the planning entry of turn 7 of the match of EXP-TURN-071, do the panels
the console's Events, Combat Summary, Ranking, Search and Hire controls open,
and the Gang Information panel of a Hire dock offer, draw the same pixels in
the rebuild as in the original, over the same city?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071 (`--scenario 0 --mentality 1 --end-turns 6 --seed 16`, with
its hires and orders), with `--white-key`, and after the dump `--order-steps
exit,exit,strip:524:150:0,shot:SCR-EVENT-001+SCR-UI-003,strip:161:304:0,strip:524:190:0,shot:SCR-COMBAT-001+SCR-UI-003,strip:161:304:0,strip:576:240:0,shot:SCR-OBJECTIVE-001+SCR-UI-003,strip:161:304:0,strip:576:266:0,shot:SCR-SEARCH-001+SCR-UI-003,strip:161:304:0,strip:524:270:0,shot:SCR-HIRE-001+SCR-UI-003,strip:185:304:0,dbl:472:405,shot:SCR-GANG-002+SCR-UI-003,strip:161:304:0`.
The two Exit steps close the panels the planning entry opened. The other
steps press the console's Events, Combat Summary, Ranking, Search and Hire
controls, each followed by a shot and the panel's Exit or OK, then
double-click the first offer of the Hire dock and press the Gang Information
panel's Exit.

The shots are taken as in EXP-UI-006 and read the lights as in EXP-UI-007.
With each shot the probe also reads the selected sector `0x004ABC80` and the
pump's counter at the moment the open panel finished sliding in: a breakpoint
at `0x004196E4` records `0x00487804` whenever the slide-in sets
`0x004854C8` (FND-UI-051). The fixture keeps it as the shot's frame counter,
the counter whose selection frame the capture shows; with no panel slid in it
is the pump counter.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071. All six shots were kept, with sector 12 selected and every
light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter |
|---|---|---|---|---|
| 3 | SCR-EVENT-001, SCR-UI-003 | 1 | 5 | 0 |
| 6 | SCR-COMBAT-001, SCR-UI-003 | 6 | 7 | 2 |
| 9 | SCR-OBJECTIVE-001, SCR-UI-003 | 10 | 1 | 4 |
| 12 | SCR-SEARCH-001, SCR-UI-003 | 3 | 3 | 6 |
| 15 | SCR-HIRE-001, SCR-UI-003 | 8 | 5 | 0 |
| 18 | SCR-GANG-002, SCR-UI-003 | 0 | 7 | 2 |

The captures show:

- the selection frame of the counter read at the slide-in (FND-UI-051); in
  the Search shot it is frame 1, where the pump counter at the capture gives
  frame 0;
- sector 12 selected, the value the original kept for the human in
  `0x004ABBF0` from the end of its previous planning (FND-SAVE-003);
- in the Search panel, each site type's icon from `PX00150` beside its name,
  cut to 15 characters, in the dim font cells when the type is not selected,
  and no keyboard border before an arrow key moves it;
- in the Hire comparison, Upkeep drawn negated in the red digits, and the
  Combat row the sum of the Combat, Strength, Fighting and Martial Arts
  fields (FND-HIRE-009);
- the portraits of the Hire comparison and the Combat Results cells scaled
  from the centre of each source pixel's span, the odd rows and columns at
  half size.

## Results

A test of the rebuild compares the captures as for EXP-UI-006, drawing the
selection frame of the recorded frame counter with the recorded sector selected.
Leaving out the cash row (DEV-UI-006) and the city's key line (DEV-UI-023),
every element of the six captures matches.

## Conclusion

The run supports SCR-EVENT-001, SCR-COMBAT-001, SCR-OBJECTIVE-001,
SCR-SEARCH-001, SCR-HIRE-001 and SCR-GANG-002 for one state, FND-UI-051 for
the held selection frame and FND-HIRE-009 for the comparison's rows.
