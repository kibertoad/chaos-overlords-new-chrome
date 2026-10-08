---
id: EXP-UI-029
title: Does the Detailed Combat panel look the same in the rebuild through the second clip of a presentation?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-029.json
---

## Question

In the state of EXP-COMBAT-005, where the police attacked the human's gangs
in sectors 42 and 51 in the last turn, does the rebuild draw the same pixels as
the original at the ticks of the second police clip the console's Detailed
Combat control replays, drawn at the clip index the probe records?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-COMBAT-005 (`--scenario 1 --mentality 0 --seed 9 --end-turns 7 --orders 1:0:3:0:0:1,2:1:3:0:0:1,3:2:3:0:0:1 --hires 1:0:51,2:0:51`), with `--order-steps
exit,exit,strip:524:218:0,shot:SCR-COMBAT-002,wait:2400,shot:SCR-COMBAT-002,wait:500,shot:SCR-COMBAT-002,wait:500,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002,wait:700,shot:SCR-COMBAT-002`.
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

The run made the same 1445 calls of `roll` as EXP-COMBAT-005. The console's
press called the presentation with argument 0, which started the two police
clips of the last turn in the order the automatic presentation played them,
on elements 0 and 2, and had not returned when the run ended. All 8 shots
were kept, with sector 51 selected, frame counter 0 and every light
byte 0:

| Step | Clip index | Clip tick | Marker frame | Pump counter |
|---|---|---|---|---|
| 3 | 0 | 4 | 10 | 5 |
| 5 | 0 | 18 | 10 | 3 |
| 7 | none | none | 4 | 7 |
| 9 | 1 | 2 | 9 | 2 |
| 11 | 1 | 6 | 4 | 6 |
| 13 | 1 | 11 | 0 | 3 |
| 15 | 1 | 15 | 8 | 7 |
| 17 | 1 | 20 | 3 | 4 |

Every shot with a tick has an index, and each index is the clip the tick's
frame belongs to: index 0 up to tick 18, index 1 from tick 2 on. Step 7 fell
between the two clips and has neither. The captures of index 1 show the second
clip's gang on the left with its own Force tracks.

## Results

A test of the rebuild presses its console control at the same point, passes
over as many clips as the shot's index, draws the next at the recorded tick and
compares the elements of SCR-COMBAT-002, and no element differs. It skips step
7, which has no tick. A second test compares the clips the console's press
started with the rebuild's first clips.

## Conclusion

The run supports SCR-COMBAT-002 for a second clip of a presentation, and the probe's clip index of FND-COMBAT-011: the index moves at the clip player's entry, before the clip's tick local is set up, so no shot paired a tick with the clip before it.
