---
id: EXP-COMBAT-003
title: Does Detailed Combat present several armed and unarmed attackers on one gang as the original does?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs switched off, Detailed Combat switched on and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-COMBAT-003.json
---

## Question

Does Detailed Combat present several armed and unarmed attackers on one gang as the original does?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-024 (`--scenario 1 --mentality 2 --turns 52 --seed 15135 --end-turns 29`), with `--detailed-combat`.
The probe sets the Detailed Combat option byte `0x0048785C` to 1, so the
human's planning entries open the presentation `0x0042E040`
(FND-COMBAT-010). At the entry of the clip player `0x00430C23` it reads the
focal gang's element number at `0x004945A0`, the other element's at
`0x00494584` and the right ends of the two bars at `0x0049476E` and
`0x004947FE` (FND-COMBAT-011), and keeps the sound numbers passed to the
loader `0x0045867C` with slot 5 since the clip before (FND-AUDIO-006,
FND-AUDIO-013). The turn loop waits while the presentation runs.

## Observations

The run made the same 16393 calls of `roll` with the same bounds and results
as EXP-TURN-024, with the 29 Done presses at the same counts. Five clips played: one at the planning entry after the twenty-eighth Done
press and four after the twenty-ninth. Every clip had the human's gang,
element 0, as focal gang and hold argument 1. In the last turn it was
attacked by elements 334, 339 and 350 of player 4 and 431 of player 5, in
that order; the armed attacker 334 loaded sound 505 and the others 500. The
focal bar showed Force 2 before the second clip and 0 before the third,
fourth and fifth.

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

The run supports RULE-COMBAT-004 for attackers listed in row order across players and a bar held at 0, and RULE-AUDIO-009 for an armed attacker.
