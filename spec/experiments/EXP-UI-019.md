---
id: EXP-UI-019
title: Does the Detailed Combat panel look the same in the rebuild through a clip of the viewer's gang attacking another gang?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-019.json
---

## Question

In the state of EXP-COMBAT-001, where the human's gang attacked gang 18 of
player 4, element 342, in sector 12 (E2) in the last turn, does the rebuild
draw the same pixels as the original at the ticks of the clip the console's
Detailed Combat control replays?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-COMBAT-001 (`--seed 61038 --end-turns 24 --orders 1:0:8:0:0:1,24:0:1:4:18:0`), with `--order-steps
strip:524:218:0,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:500,shot:SCR-COMBAT-002,wait:250,shot:SCR-COMBAT-002,wait:250,shot:SCR-COMBAT-002,wait:500,shot:SCR-COMBAT-002`.
After the dump the probe presses the console's Detailed Combat control, which
calls the presentation `0x0042E040` with its second argument 0 and replays the
last turn's clips from the first (RULE-COMBAT-004). The `wait` steps pump the
original's messages for the given milliseconds with no input, and each `shot`
copies the drawing area as in EXP-UI-009. Around each copy the probe reads the
clip's tick, the local at `ebp - 0x20` of the clip player `0x00430C23`, less
1 (FND-COMBAT-016), from the frame it saw the clip set that local up at
`0x00430C2F`, and copies again when the tick moved during the copy. The run
adds `--detailed-combat`, which the probe applies as in EXP-COMBAT-001, and
`--white-key`.

## Observations

The run made the same 12623 calls of `roll` as EXP-COMBAT-001. The console's
press called the presentation with argument 0, which started the clip of
element 0 on element 342 with the same gangs, hold argument, bars and sound as
the automatic presentation, and had not returned when the run ended. All six
shots were kept, with sector 12 selected, frame counter 7 and every light
byte 0:

| Step | Clip tick | Marker frame | Pump counter |
|---|---|---|---|
| 1 | 4 | 4 | 4 |
| 3 | 8 | 0 | 0 |
| 5 | 11 | 5 | 3 |
| 7 | 13 | 8 | 5 |
| 9 | 15 | 10 | 7 |
| 11 | 18 | 3 | 2 |

The captures show the strips' frames 1 and 5, the last frames on tick 11,
those frames darkened with the part of element 0's lower track it lost in the
fight white on ticks 13 and 15, and that track lowered on tick 18. The sector tile is the
cell of the map art without player 0's colour, under a black frame.

## Results

A test of the rebuild presses the rebuild's console control at the same point,
draws its clip at the recorded tick and compares the elements of SCR-COMBAT-002,
and no element differs. Another test compares the clip the console's press
started with the rebuild's first clip.

## Conclusion

The run supports SCR-COMBAT-002 for a clip of the viewer's gang on another gang through frames, darkening, both white ticks and the held result, FND-COMBAT-016, and FND-COMBAT-017.
