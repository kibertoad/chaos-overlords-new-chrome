---
id: EXP-COMBAT-009
title: Does Detailed Combat present an attack of the viewer's that the target evades as the original does?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-COMBAT-009.json
---

## Question

Does Detailed Combat present an attack of the viewer's that the target evades as the original does?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-013 (`--scenario 7 --seed 46976`) with `--end-turns 10 --orders 1:0:8:0:0:1,8:0:10:21:0:0,9:0:10:12:0:0,10:0:1:3:0:0`: the human's gang hides until it moves to sector 21 and then 12, and in the tenth turn attacks gang 0 of player 3, element 243, which hides in that turn. The run adds `--detailed-combat`.
The probe sets the Detailed Combat option byte `0x0048785C` to 1, so the
human's planning entries open the presentation `0x0042E040`
(FND-COMBAT-010). At the entry of the clip player `0x00430C23` it reads the
focal gang's element number at `0x004945A0`, the other element's at
`0x00494584` and the right ends of the two bars at `0x0049476E` and
`0x004947FE` (FND-COMBAT-011), and keeps the sound numbers passed to the
loader `0x0045867C` with slot 5 since the clip before (FND-AUDIO-006,
FND-AUDIO-013). The turn loop waits while the presentation runs.

## Observations

The run made 1794 calls of `roll` and 10 Done presses. One clip played,
after the tenth press: focal element 0, other element 243, hold argument 1,
the bars at Force 10 and 10, and sound 499 loaded for the evaded attack.

At the clip player's entry the bars' right ends give the Force shown before
that clip's damage, `256 + 6 * force_shown` for the focal gang and
`329 + 6 * force_shown` for the other.

## Results

`DetailedCombatPlaysTheOriginalsClips` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.DetailedCombat.cs` replays
the run and, at each of the human's planning entries, builds the rebuild's
automatic presentation of the last turn's fights. Its clips have the same
focal and other gangs, hold argument, bar ends and slot-5 sound, in the same
order. `TheRebuildStartsTheSameMatch` in
`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the rolls.

## Conclusion

The run supports RULE-COMBAT-004 for the viewer's attack that its target evades, and RULE-AUDIO-009 for its sound number.
