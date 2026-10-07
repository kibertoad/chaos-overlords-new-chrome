---
id: EXP-COMBAT-004
title: Does Detailed Combat give a bare-handed Martial Arts attack the original's sound?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-COMBAT-004.json
---

## Question

Does Detailed Combat give a bare-handed Martial Arts attack the original's sound?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-029 (`--seed 369 --end-turns 10 --orders 1:0:8:0:0:1,2:1:8:0:0:1,10:1:1:5:2:0 --hires 1:0:9`), with `--detailed-combat`.
The probe sets the Detailed Combat option byte `0x0048785C` to 1, so the
human's planning entries open the presentation `0x0042E040`
(FND-COMBAT-010). At the entry of the clip player `0x00430C23` it reads the
focal gang's element number at `0x004945A0`, the other element's at
`0x00494584` and the right ends of the two bars at `0x0049476E` and
`0x004947FE` (FND-COMBAT-011), and keeps the sound numbers passed to the
loader `0x0045867C` with slot 5 since the clip before (FND-AUDIO-006,
FND-AUDIO-013). The turn loop waits while the presentation runs.

## Observations

The run made the same 2572 calls of `roll` with the same bounds and results
as EXP-TURN-029, with the 10 Done presses at the same counts. One clip played, at the planning entry after the tenth Done press: focal
element 1, the hired gang with Martial Arts above 0 and no weapon, and other
element 407, its target, with hold argument 1. The bars showed Force 5 and
8, and the loader was passed sound 501.

At the clip player's entry the bars' right ends give the Force shown before
that clip's damage, `256 + 6 * force_shown` for the focal gang and
`329 + 6 * force_shown` for the other.

## Results

A test of the rebuild replays the run and, at each of the human's planning
entries, builds the rebuild's automatic presentation of the last turn's fights.
Its clips have the same focal and other gangs, hold argument, bar ends and
slot-5 sound, in the same order. Another test replays the rolls.

## Conclusion

The run supports RULE-AUDIO-009 for a bare-handed attacker with Martial Arts.
