---
id: EXP-COMBAT-007
title: Does Detailed Combat present the viewer's fights in two sectors as the original does?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-COMBAT-007.json
---

## Question

Does Detailed Combat present the viewer's fights in two sectors as the original does?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-066 (`--scenario 1 --mentality 0 --seed 9 --hires 1:0:51,2:0:51`) with `--end-turns 12 --orders 1:0:10:42:0:0,2:0:3:0:0:1,2:1:3:0:0:1,3:2:3:0:0:1`: the human's first gang moves to sector 42 in the first turn and takes up Chaos there in the second, while the two hired gangs take up Chaos in sector 51. The run adds `--detailed-combat`.
The probe sets the Detailed Combat option byte `0x0048785C` to 1, so the
human's planning entries open the presentation `0x0042E040`
(FND-COMBAT-010). At the entry of the clip player `0x00430C23` it reads the
focal gang's element number at `0x004945A0`, the other element's at
`0x00494584` and the right ends of the two bars at `0x0049476E` and
`0x004947FE` (FND-COMBAT-011), and keeps the sound numbers passed to the
loader `0x0045867C` with slot 5 since the clip before (FND-AUDIO-006,
FND-AUDIO-013). The turn loop waits while the presentation runs.

## Observations

The run made 3007 calls of `roll` and 12 Done presses. Four police clips
played: on element 1 after the tenth and eleventh presses, and after the
twelfth on element 0, in sector 42, and then element 2, in sector 51. Each
had other element -2, hold argument 1, the other bar at Force 10 and sound
518.

At the clip player's entry the bars' right ends give the Force shown before
that clip's damage, `256 + 6 * force_shown` for the focal gang and
`329 + 6 * force_shown` for the other.

## Results

A test of the rebuild replays the run and, at each of the human's planning
entries, builds the rebuild's automatic presentation of the last turn's fights.
Its clips have the same focal and other gangs, hold argument, bar ends and
slot-5 sound, in the same order. Another test replays the rolls.

## Conclusion

The run supports RULE-COMBAT-004 for a presentation over two sectors, in ascending sector order.
