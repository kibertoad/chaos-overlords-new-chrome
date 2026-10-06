---
id: EXP-UI-049
title: Does the Detailed Combat panel look the same in the rebuild through two evaded attacks on the viewer's gang?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-049.json
---

## Question

In the state of EXP-COMBAT-002, where two gangs of player 3 attacked the
human's hiding gang in the last turn and it evaded both, does the rebuild draw
the same pixels as the original at the ticks of the two clips the console's
Detailed Combat control replays?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-COMBAT-002 (`--scenario 6 --mentality 2 --seed 11541 --end-turns 23 --orders 1:0:8:0:0:1`), with `--order-steps
exit,exit,strip:524:218:0,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:600,shot:SCR-COMBAT-002,wait:300,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:1000,shot:SCR-COMBAT-002,wait:500,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:600,shot:SCR-COMBAT-002,wait:400,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002`.
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

EXP-COMBAT-002 took the Mentality the setup screen opened with; this run
gives it, the one the end state of EXP-COMBAT-002 records. It made the same
9778 calls of `roll` as EXP-COMBAT-002. The console's press called the
presentation with argument 0, which started the clips of elements 258 and 259
on element 0, both with sound 499, as the automatic presentation had, and had
not returned when the run ended. All 11 shots were kept, with sector
9 selected, frame counter 3 and every light byte 0:

| Step | Clip index | Clip tick | Marker frame | Pump counter |
|---|---|---|---|---|
| 3 | 0 | 4 | 9 | 7 |
| 5 | 0 | 8 | 4 | 3 |
| 7 | 0 | 12 | 11 | 7 |
| 9 | 0 | 14 | 2 | 1 |
| 11 | 0 | 19 | 9 | 6 |
| 13 | 1 | 2 | 8 | 4 |
| 15 | 1 | 5 | 1 | 7 |
| 17 | 1 | 10 | 9 | 4 |
| 19 | 1 | 14 | 3 | 0 |
| 21 | 1 | 16 | 8 | 2 |
| 23 | 1 | 21 | 3 | 7 |

The captures show the mirrored strips `PX07227` and `PX07300`. In the capture
of step 13, tick 2 of the second clip, both apertures are black, where
EXP-UI-046 and EXP-UI-054 show frame 0 of the strips at ticks 0 to 2. The
rest of that capture shows the second clip's gangs.

## Results

`TheRebuildDrawsWhatTheOriginalDrew` in
`tests/Rechaos.Tests/ScreenCaptureTests.cs` presses the rebuild's console
control at the same point, draws each shot's clip at its index and tick and
compares the elements of SCR-COMBAT-002. No element differs, apart from step
13, which the test skips: the rebuild draws frame 0 there, as the original
does without a paint (FND-COMBAT-032, EXP-UI-054).
`TheConsolesDetailedCombatControlPlaysTheLastTurnAgain` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.DetailedCombat.cs`
compares the clips the console's press started with the rebuild's first clips.

## Conclusion

The run supports SCR-COMBAT-002 for evaded attacks on the viewer's gang. The black apertures of step 13 are what FND-COMBAT-032 reads a paint before tick 3 to leave; the run did not record paints, so that explanation is not checked against this run.
