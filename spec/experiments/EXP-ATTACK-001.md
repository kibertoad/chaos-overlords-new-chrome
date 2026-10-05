---
id: EXP-ATTACK-001
title: Does the Attack picker leave out an enemy gang in the sector that the player does not see?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-ATTACK-001.json
---

## Question

No recorded run reads the targets the Attack picker offers. When the only
enemy gang in the acting gang's sector is one the player does not see, does
the picker list nothing?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-072, with `--attack-lists` added
(`--scenario 0 --mentality 1 --seed 2 --end-turns 30 --orders 29:0:10:61:0:0,30:0:5:40:0:0 --attack-lists`).

After the end state is dumped, the probe stops the original at the next
`PeekMessageA` call of the message pump (FND-UI-020), saves the thread
context and calls the picker's roster builder `fn_0043D132` of FND-ATTACK-006
once for each other player and each living gang of the human, passing that
player and the gang's sector, as the picker does when an opponent is chosen.
After each call it reads the six entries at `0x00494850` and keeps those that
are not -1, in entry order. It then restores the saved context.

## Observations

The run made the same 13699 calls of `roll` with the same bounds and results
as EXP-TURN-072, and every value of EXP-TURN-072's end state is the same.

The human has one gang, in sector 61. The only other gang there is roster
slot 7 of player 4, whose `visible_to` entry for the human is 0. All five
lists are empty.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.Presentation.cs`
(`TheAttackPickerOffersTheOriginalsTargets`) replays the run and, for each
opponent, compares the gangs the rebuild's Attack picker shows, as roster
slots, with the recorded targets. The rebuild shows none for each opponent.

## Conclusion

The run agrees with RULE-ATTACK-002 for an enemy gang in the sector that the
player does not see.
