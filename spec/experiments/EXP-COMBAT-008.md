---
id: EXP-COMBAT-008
title: Does the console's Detailed Combat control with no fight to show do what the original does?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-COMBAT-008.json
---

## Question

Does the console's Detailed Combat control with no fight to show do what the original does?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-011 (`--seed 61038 --orders 1:0:8:0:0:1`) with `--end-turns 23 --order-steps strip:524:218:0`: the human's gang hides through 23 turns, and after the dump the probe presses the console's Detailed Combat control. The run adds `--detailed-combat`.
The probe sets the Detailed Combat option byte `0x0048785C` to 1, so the
human's planning entries open the presentation `0x0042E040`
(FND-COMBAT-010). At the entry of the clip player `0x00430C23` it reads the
focal gang's element number at `0x004945A0`, the other element's at
`0x00494584` and the right ends of the two bars at `0x0049476E` and
`0x004947FE` (FND-COMBAT-011), and keeps the sound numbers passed to the
loader `0x0045867C` with slot 5 since the clip before (FND-AUDIO-006,
FND-AUDIO-013). The turn loop waits while the presentation runs.
For each call of the presentation the probe also keeps its second argument,
1 when planning opened it and 0 when the console's control did, the clips it
played, and the effect slots it played itself through `0x00464290` from
within `0x0042E040`..`0x0042EE46` (FND-COMBAT-010).

## Observations

The run made 11599 calls of `roll` and 23 Done presses. No clip played. The
presentation was called at each planning entry with argument 1 and played
no sound. The console's press called it with argument 0; it played effect
slot 4 and returned without a clip.

At the clip player's entry the bars' right ends give the Force shown before
that clip's damage, `256 + 6 * force_shown` for the focal gang and
`329 + 6 * force_shown` for the other.

## Results

A test of the rebuild replays the run and, at each of the human's planning
entries, builds the rebuild's automatic presentation of the last turn's fights.
Its clips have the same focal and other gangs, hold argument, bar ends and
slot-5 sound, in the same order. Another test replays the rolls. A third test
checks the presentation the console's control opened against the rebuild's
control at `(500, 211, 48, 15)` (SCR-UI-003).

## Conclusion

The run supports RULE-COMBAT-004 for a presentation with no sector to show, silent when planning opens it and playing effect slot 4 when the console's control does.
