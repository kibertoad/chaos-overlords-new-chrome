---
id: EXP-UI-025
title: Which copies does the original make when a panel slides in?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off, Slide Panels on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-025.json
---

## Question

With Slide Panels on, at which offsets does the panel-open helper copy a panel
in the alternate and in the primary form, and which step does the startup
benchmark give?

## Setup

As EXP-TURN-001.

## Procedure

`Rechaos.OriginalProbe new-game --scenario 0 --mentality 1 --humans 0 --seed 16
--end-turns 1 --slides --order-steps
exit,exit,strip:524:270:0,wait:600,strip:185:304:0,wait:400,open:12,wait:600,card:0:20:12:10,wait:600,strip:161:272:0,wait:400,back,wait:400`.

From the dump on, the probe records each call of the panel-open helper
`fn_0041953E` (FND-UI-011) with the startup benchmark count at `0x004981F8`,
and at each copy of its slide loop, the call at `0x0041965D`, the travel at
`ebp - 4` and the width shown at `ebp - 8`; the last copy is the call at
`0x004196DC`. The steps open the Hire panel from the console and close it, then
open sector 12, press the first card's Move entry, cancel the Move panel and go
back to the city.

## Observations

The benchmark count was 21892, so the divisor was 5473 and both travels took
the 16-pixel minimum step. The Hire panel slid in the alternate form over 320
pixels in 20 copies, at offsets 304 down to 16 in steps of 16 and then 0. The
Move panel slid in the primary form over 344 pixels in 22 copies, at offsets
328 down to 8 and then 0.

## Results

`PanelsSlideInWithTheOriginalsCopies` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Slides.cs` compares the
rebuild's step and copy sequence for the recorded benchmark with each slide,
and the offsets the rebuild's Hire screen and order panel show at its fixed pace
of 84 copies a second (DEV-TIMER-001). A first comparison found the rebuild's
order panels, which open on its Commands screen, not sliding at all.

## Conclusion

The run supports RULE-UI-003 for the slide-in of a primary and an alternate
panel on a machine fast enough for the minimum step. The slide-out was not
recorded; the rebuild does not animate it (DEV-UI-001).
