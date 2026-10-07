---
id: EXP-UI-020
title: Does the Detailed Combat panel look the same in the rebuild through a police clip?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-020.json
---

## Question

In the state of EXP-COMBAT-005, where the police attacked the human's gangs in
sectors 42 and 51 in the last turn, does the rebuild draw the same pixels as
the original at the ticks of the first police clip the console's Detailed
Combat control replays?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-COMBAT-005 (`--scenario 1 --mentality 0 --seed 9 --end-turns 7 --orders 1:0:3:0:0:1,2:1:3:0:0:1,3:2:3:0:0:1 --hires 1:0:51,2:0:51`), with `--order-steps
exit,strip:524:218:0,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:600,shot:SCR-COMBAT-002,wait:300,shot:SCR-COMBAT-002,wait:900,shot:SCR-COMBAT-002`.
The `exit` step closes Last Turn Events, which the planning entry opened over
the console.
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

The run made the same 1445 calls of `roll` as EXP-COMBAT-005. The console's
press called the presentation with argument 0, which started the police clip
on element 0 with the same bars and sound as the automatic presentation, and
had not returned when the run ended. All five shots were kept, with sector 51
selected, frame counter 2 and every light byte 0:

| Step | Clip tick | Marker frame | Pump counter |
|---|---|---|---|
| 2 | 4 | 2 | 7 |
| 4 | 9 | 9 | 4 |
| 6 | 12 | 4 | 0 |
| 8 | 14 | 7 | 2 |
| 10 | 20 | 5 | 7 |

The captures show the police header and pictures on the right. On tick 14 the
lower track of element 0 shows the Force before the clip, which the capture of
tick 20 shows lowered.

## Results

A test of the rebuild presses the rebuild's console control at the same point,
draws its clip at the recorded tick and compares the elements of SCR-COMBAT-002,
and no element differs. Another test compares the clip the console's press
started with the rebuild's first clip.

## Conclusion

The run supports SCR-COMBAT-002 for a police clip, the police art of FND-COMBAT-014, and FND-COMBAT-016 for the track shown on tick 14.
