---
id: EXP-TURN-049
title: What planning state do the computer players hold after twenty-four turns of Kill 'Em All at Crime Lord?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 3
fixture: EXP-TURN-049.json
---

## Question

As EXP-TURN-048, for a scenario whose hire table assigns families 1, 2, 5 and
7 as well as 0: does the original hold the planning records, focus and
coverage values, sector weights and per-player values the rebuild holds?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-048 with Kill 'Em All (`--scenario 4`), one year
(`--turns 52`), and `--seed 4901`, `--seed 4902` and `--seed 4903`.

## Observations

The runs made 12614, 12482 and 11539 calls of `roll`. The planning records
at the end hold Move, Equip, Influence, Research, Heal, Control and Chaos,
planned by families 0, 1, 2, 5 and 7.

## Results

A test of the rebuild replays the runs. The rebuild makes the same calls with
the same bounds and results, reaches the same state, and holds the same planning
record bytes, sector weights, per-player values and, for each computer gang
whose family is assigned, focus and coverage sector.

## Conclusion

The runs agree with FND-AI-074 for the target bytes of Move, Equip, Influence
and Research and for family 5's focus: the gang's sector after an Influence
or Attack, -1 after a Heal, Move, Equip or failed draw.
