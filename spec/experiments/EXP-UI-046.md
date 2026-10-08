---
id: EXP-UI-046
title: Does the Detailed Combat panel look the same in the rebuild through an armed and an unarmed attack on the viewer's gang?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-UI-046.json
---

## Question

In the state of EXP-COMBAT-003, where four gangs attacked the human's gang in
the last turn, the first with a weapon, does the rebuild draw the same pixels
as the original at the ticks of the first two clips the console's Detailed
Combat control replays?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-COMBAT-003 (`--scenario 1 --mentality 2 --turns 52 --seed 15135 --end-turns 29`), with `--order-steps
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

The run made the same 16393 calls of `roll` as EXP-COMBAT-003. The console's
press called the presentation with argument 0, which started the clips of
elements 334 and 339 on element 0, as the automatic presentation had, and had
not returned when the run ended. The first clip loaded sound 505 for its
armed attacker, the second sound 500 for an unarmed attacker of gang
definition 63 (FMT-DATA-002) whose base Martial Arts is 0 or less
(FND-AUDIO-013). All 11 shots were kept, with sector 54 selected,
frame counter 0 and every light byte 0:

| Step | Clip index | Clip tick | Marker frame | Pump counter |
|---|---|---|---|---|
| 3 | 0 | 4 | 5 | 5 |
| 5 | 0 | 9 | 1 | 2 |
| 7 | 0 | 12 | 7 | 5 |
| 9 | 0 | 14 | 10 | 7 |
| 11 | 0 | 19 | 6 | 4 |
| 13 | 1 | 2 | 4 | 2 |
| 15 | 1 | 5 | 10 | 5 |
| 17 | 1 | 10 | 5 | 2 |
| 19 | 1 | 14 | 0 | 6 |
| 21 | 1 | 16 | 4 | 0 |
| 23 | 1 | 21 | 0 | 5 |

The captures show the attacker on the right in mirrored strips. The second
clip's attack strip is `PX07202`, attack strip 2, with hit strip `PX07302`,
the pair FND-COMBAT-010 gives definition 63, in place of `PX07200` for an
unarmed attacker without Martial Arts. Its frame 0 is on the screen at tick 2.

## Results

A test of the rebuild presses its console control at the same point, draws each
shot's clip at its index and tick and compares the elements of SCR-COMBAT-002,
and no element differs. Before the rebuild took attack strip 2 for definition
63, the attack strip of every shot of the second clip differed. A second test
compares the clips the console's press started with the rebuild's first clips.

## Conclusion

The run supports SCR-COMBAT-002 for attacks on the viewer's gang, armed and unarmed, the strip of definition 63 that FND-COMBAT-010 and FND-COMBAT-032 read, and the first frames shown before tick 3 (FND-COMBAT-032).
