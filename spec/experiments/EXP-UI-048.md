---
id: EXP-UI-048
title: Does the Detailed Combat panel look the same in the rebuild through an attack of the viewer's that its target evades?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-048.json
---

## Question

In the state of EXP-COMBAT-009, where the human's gang attacked element 243,
which evaded it, in the last turn, does the rebuild draw the same pixels as the
original at the ticks of the clip the console's Detailed Combat control
replays?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-COMBAT-009 (`--scenario 7 --mentality 2 --seed 46976 --end-turns 10 --orders 1:0:8:0:0:1,8:0:10:21:0:0,9:0:10:12:0:0,10:0:1:3:0:0`), with `--order-steps
exit,exit,strip:524:218:0,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:600,shot:SCR-COMBAT-002,wait:300,shot:SCR-COMBAT-002,wait:250,shot:SCR-COMBAT-002,wait:500,shot:SCR-COMBAT-002`.
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

EXP-COMBAT-009 took the Mentality the setup screen opened with; this run
gives it, the one the end state of EXP-COMBAT-009 records. It made the same
1794 calls of `roll` as EXP-COMBAT-009. The console's press called the
presentation with argument 0, which started the clip of element 0 on element
243 with sound 499, as the automatic presentation had, and had not returned
when the run ended. All 6 shots were kept, with sector 30 selected,
frame counter 0 and every light byte 0:

| Step | Clip index | Clip tick | Marker frame | Pump counter |
|---|---|---|---|---|
| 3 | 0 | 4 | 6 | 5 |
| 5 | 0 | 9 | 2 | 2 |
| 7 | 0 | 12 | 8 | 5 |
| 9 | 0 | 14 | 11 | 7 |
| 11 | 0 | 16 | 2 | 1 |
| 13 | 0 | 20 | 8 | 5 |

The captures show the strips `PX07027` and `PX07100`.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` presses the rebuild's console
control at the same point, draws its clip at the recorded tick and compares
the elements of SCR-COMBAT-002, and no element differs.
`TheConsolesDetailedCombatControlPlaysTheLastTurnAgain` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.DetailedCombat.cs`
compares the clips the console's press started with the rebuild's first clips.

## Conclusion

The run supports SCR-COMBAT-002 for an evaded attack by the viewer's gang.
