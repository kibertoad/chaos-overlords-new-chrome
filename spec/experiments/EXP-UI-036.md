---
id: EXP-UI-036
title: What does the drawing area show at the state dump when the planning entry has a report?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-036.json
---

## Question

The probe's `--capture` copies the drawing area at the state dump. When the
planning entry has a last-turn report to show, does that copy show the city
screen, or Last Turn Events (SCR-EVENT-001) still open on its first report?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-UI-020 (`--scenario 1 --mentality 0 --seed 9 --end-turns 7 --orders
1:0:3:0:0:1,2:1:3:0:0:1,3:2:3:0:0:1 --hires 1:0:51,2:0:51 --detailed-combat
--white-key`), with `--capture` and no order steps. In the last turn the police
attacked the human's gangs in sector 51 (EXP-COMBAT-005), so the planning entry
of turn 8 presents the clips and then has one Crackdown report to show.

## Observations

The run made the 1445 calls of `roll` of EXP-COMBAT-005. The probe recorded
Last Turn Events as shown after the Done press of every turn from the fourth,
the last after call 1445.

The copy shows Last Turn Events open over the city on its first page, `01 OF
01`, with the police illustration and the Crackdown caption for sector D7,
which the capture has selected. Behind the panel the city and the console are
drawn; the console's Tolerance row shows 13 in the plain colour. The selection
frame around the selected sector is drawn white, while the capture records the
pump's counter as 5. Marker frame 10, every light byte 0.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` draws the replayed endpoint with
`--entry-panels`, which leaves Last Turn Events open on its first page as at
the dump. The panel's page number, page count, illustration and footer match,
and so does every element of SCR-UI-003 except the city map. The Tolerance row
is masked, since the rebuild draws it orange there under DEV-UI-007. The city
map, the whole screen and the panel's whole-screen element differ only in the
selection frame. They
are reported and not asserted, since the frame the panel held when it came in
(FND-UI-051) is not what the pump's counter at the dump gives, and the capture
does not record it.

## Conclusion

The run shows that a capture at the dump stands before the Exit of the planning
entry's Last Turn Events, which supports SCR-EVENT-001 for a panel opened by
the planning entry. Which selection frame the panel holds when the planning
entry opens it is not settled.
