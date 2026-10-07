---
id: EXP-HIRE-002
title: Does a hire order move between sectors, and is it set when the player cannot pay?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, full screen switched off in memory, silent, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-HIRE-002.json
---

## Question

Does dragging an offer already ordered to hire onto another accepted sector
move the order, is a sector where the player has a gang but no ownership
accepted, and does the dock leave cash untested?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-071, with `--hire-steps exit,exit,drag:0:19,drag:0:12,drag:1:20,reject:0,drag:2:19`
added. The steps are taken as in EXP-HIRE-001; `exit` presses the Exit control
of a result panel (`(161, 304)`), as the probe does between turns, because
the run ends with Combat Results open. The probe keeps the panel calls as they
stood at the dump.

## Observations

The run made the same 1182 calls of `roll` with the same bounds and results
as EXP-TURN-071, and the same panel calls. The human owns sector 12, all three
of its gangs stand in sector 19, which has no owner, and sector 20 is neither
owned by it nor holds a gang of it. Its cash is -32, below the price of every
offer. Its three order bytes after each step are:

| Step | Orders |
|---|---|
| exit | -1, -1, -1 |
| exit | -1, -1, -1 |
| drag 0 to 19 | 19, -1, -1 |
| drag 0 to 12 | 12, -1, -1 |
| drag 1 to 20 | 12, -1, -1 |
| reject 0 | -1, -1, -1 |
| drag 2 to 19 | -1, -1, 19 |

The other fifteen bytes stay -1.

## Results

A test of the rebuild compares the run as in EXP-HIRE-001, and the orders are
the same after every step.

## Conclusion

The run agrees with RULE-HIRE-003: a drop is accepted on a sector where the
player has a gang, a hire order moves to the sector it is dropped on next,
and the dock sets orders the player cannot pay for.
