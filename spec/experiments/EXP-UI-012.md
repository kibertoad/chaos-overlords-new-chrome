---
id: EXP-UI-012
title: Does the idle gang warning look the same in the rebuild?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory until the steps after the dump, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-012.json
---

## Question

At the planning entry of the match of EXP-UI-006, where the human's gang has
no order, does the warning a Done press opens with Warn if Idle Gangs on draw
the same pixels in the rebuild as in the original?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-UI-006 (`--scenario 9 --mentality 1 --seed 5490 --end-turns 28
--white-key`), with `--order-steps
exit,exit,exit,warn,strip:550:306:0,shot:SCR-OPTIONS-001+SCR-UI-003,strip:161:272:0`.
The `warn` step writes 1 to Warn if Idle Gangs at `0x00487860`
(FND-OPTIONS-001), which the run switched off before its Done presses; the
next step presses the console's Done, and the last the warning's Cancel. The
shot is taken and its counters read as in EXP-UI-009. While the warning's
handler runs, the probe reads its tick count `[ebp-0x2c]` and shown flag
`[ebp-0xc]` (FND-UI-054) before and after the capture, from the frame pointer
it records at `0x00448730`, and keeps the ticks since the open modulo 8 as the
shot's item frame when the two reads agree.

## Observations

The run made the same 14892 calls of `roll` with the same bounds and results
as EXP-TURN-026. The Done press opened the warning, and the shot was kept with
sector 33 selected and every light byte 0:

| Step | Screens | Marker frame | Pump counter | Frame counter | Item frame |
|---|---|---|---|---|---|
| 5 | SCR-OPTIONS-001, SCR-UI-003 | 9 | 0 | 3 | 5 |

The capture shows the warning panel over the city, with its red line shown.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` compares the capture as for
EXP-UI-009, with the rebuild's Warn if Idle Gangs at its default, on, and the
recorded item frame as the line's phase. Leaving out the cash row (DEV-UI-006)
and the city's key line (DEV-UI-023), every element matches. A first run of
the comparison, which blinked the line on the presentation clock's ticks since
the program started, drew the line black.

## Conclusion

The run supports SCR-OPTIONS-001 and RULE-OPTIONS-003 for one state with an
idle gang, and FND-UI-054 for the line shown five ticks after the open.
