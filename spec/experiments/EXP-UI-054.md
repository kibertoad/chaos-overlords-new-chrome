---
id: EXP-UI-054
title: What do the Detailed Combat apertures show before the second clip's first strip frame?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-054.json
---

## Question

In the state of EXP-COMBAT-002, do the apertures show the strips' frame 0 at
ticks 0 to 2 of the second evaded clip the console's Detailed Combat control
replays, and does a paint of the window come during the presentation?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-UI-049 (`--scenario 6 --mentality 2 --seed 11541 --end-turns 23 --orders 1:0:8:0:0:1 --trace-calls 0x0045CD70`), with `--order-steps
exit,exit,strip:524:218:0,shot:SCR-COMBAT-002,wait:2500,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002,wait:120,shot:SCR-COMBAT-002`.
After the dump the probe presses the console's Detailed Combat control, which
calls the presentation `0x0042E040` with its second argument 0 and replays the
last turn's clips from the first (RULE-COMBAT-004). An `exit` step with no
result panel open is skipped. The `wait` steps pump the original's messages
for the given milliseconds with no input, and each `shot` copies the drawing
area as in EXP-UI-009. Around each copy the probe reads the clip's tick, the
local at `ebp - 0x20` of the clip player `0x00430C23` less 1, from the frame
it saw the clip set that local up at `0x00430C2F` (FND-COMBAT-016), and the
clip's index within the presentation, the number of clips the presentation
has started less 1, counted at the clip player's entry (FND-COMBAT-011). It
copies again when either moved during the copy. When a clip returns at
`0x00431C53` the probe forgets its frame, so a shot taken before the next
clip sets its tick up keeps neither value. The run adds `--detailed-combat`,
which the probe applies as in EXP-COMBAT-001, and `--white-key`.

## Observations

The run made the same 9778 calls of `roll` as EXP-COMBAT-002, and the same
presentations and clips as EXP-UI-049. `--trace-calls 0x0045CD70` noted one
call of the `BeginPaint` wrapper (FND-UI-020), from `0x0045C71A` before the
first roll, and none from the clip player's paint branch `0x00430FF3`
(FND-COMBAT-032). All 14 shots were kept, with sector 9 selected,
frame counter 5 and every light byte 0:

| Step | Clip index | Clip tick | Marker frame | Pump counter |
|---|---|---|---|---|
| 3 | 0 | 4 | 7 | 2 |
| 5 | 0 | 19 | 8 | 1 |
| 7 | 0 | 20 | 10 | 2 |
| 9 | 0 | 21 | 0 | 3 |
| 11 | 1 | 0 | 1 | 4 |
| 13 | 1 | 0 | 3 | 5 |
| 15 | 1 | 1 | 5 | 6 |
| 17 | 1 | 2 | 6 | 7 |
| 19 | 1 | 3 | 8 | 0 |
| 21 | 1 | 3 | 9 | 0 |
| 23 | 1 | 5 | 11 | 2 |
| 25 | 1 | 6 | 1 | 3 |
| 27 | 1 | 7 | 3 | 4 |
| 29 | 1 | 8 | 4 | 5 |

Ticks 19 to 21 of the first clip show both last frames darkened. At ticks 0
to 3 of the second clip the apertures show frame 0 of `PX07300` and
`PX07227`, the same pixels at each of those ticks; ticks 5 to 8 show frames 2
to 5.
The trace notes do not go into the fixture.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` presses the rebuild's console
control at the same point, draws each shot's clip at its index and tick and
compares the elements of SCR-COMBAT-002, and no element differs.
`TheConsolesDetailedCombatControlPlaysTheLastTurnAgain` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.DetailedCombat.cs`
compares the clips the console's press started with the rebuild's first clips.

## Conclusion

The run supports FND-COMBAT-032: without a paint the apertures show frame 0 of the strips from the clip's setup until tick 3, and SCR-COMBAT-002 for the ticks before the first frame of an evaded clip.
