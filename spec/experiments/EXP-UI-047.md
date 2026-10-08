---
id: EXP-UI-047
title: Does the Detailed Combat panel look the same in the rebuild through a bare-handed Martial Arts attack?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-047.json
---

## Question

In the state of EXP-COMBAT-004, where the human's hired gang, bare-handed and
with base Martial Arts above 0, attacked element 407 in the last turn, does the
rebuild draw the same pixels as the original at the ticks of the clip the
console's Detailed Combat control replays?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-COMBAT-004 (`--scenario 4 --mentality 2 --seed 369 --end-turns 10 --orders 1:0:8:0:0:1,2:1:8:0:0:1,10:1:1:5:2:0 --hires 1:0:9`), with `--order-steps
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

EXP-COMBAT-004 took the scenario and Mentality the setup screen opened with;
this run gives them, the ones the end state of EXP-COMBAT-004 records. It made
the same 2572 calls of `roll` as EXP-COMBAT-004. The console's press called
the presentation with argument 0, which started the clip of element 1 on
element 407 with sound 501, as the automatic presentation had, and had not
returned when the run ended. All 6 shots were kept, with sector 9
selected, frame counter 2 and every light byte 0:

| Step | Clip index | Clip tick | Marker frame | Pump counter |
|---|---|---|---|---|
| 3 | 0 | 4 | 10 | 7 |
| 5 | 0 | 8 | 5 | 3 |
| 7 | 0 | 12 | 11 | 7 |
| 9 | 0 | 14 | 2 | 1 |
| 11 | 0 | 16 | 6 | 3 |
| 13 | 0 | 19 | 11 | 6 |

The captures show the strips `PX07001` and `PX07118`.

## Results

A test of the rebuild presses its console control at the same point, draws its
clip at the recorded tick and compares the elements of SCR-COMBAT-002, and no
element differs. A second test compares the clips the console's press started
with the rebuild's first clips.

## Conclusion

The run supports SCR-COMBAT-002 for the strips of a bare-handed attacker with Martial Arts.
