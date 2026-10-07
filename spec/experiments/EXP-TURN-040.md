---
id: EXP-TURN-040
title: How do raiders and family-4 gangs plan, when the probe sets them in memory?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-040.json
---

## Question

Family 9 plans for a player whose `raider_mode` is set, which only a computer
player taking over a network seat does (RULE-AI-027), and no match assigns
family 4 (RULE-AI-023). Neither handler runs in a local match. How do the two
plan when the flag and the family are written into the original's memory?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 17`, `--scenario 1` (Power) and twelve Done
presses (`--end-turns 12`). The human gives no orders. Before the first Done
press the probe sets the `raider_mode` byte at `0x00482158` for players 1 and 3
(`--raiders 1:1,1:3`). Before the second it writes 4 into the `family` of the
planning record in roster slot 0 of players 2, 4 and 5 (FMT-STATE-007,
`--families 2:2:0:4,2:4:0:4,2:5:0:4`); the first planning pass of each player
has assigned that record a family by then, so the write is not reset.

The seed was found in the rebuild, which played this setup for seeds 1 to 40
in Power and Kill 'Em All and counted each family-4 and family-9 plan by the
previous and the planned action. Seed 17 gave the most kinds of plan,
including family-9 attacks.

## Observations

The run made 1857 calls of `roll`, with the Done presses at the counts listed
in the fixture. The raiders' gangs equip, move out of their own sectors, take
other sectors by Control and attack, and the family-4 gangs raise Chaos, equip
and move.

## Results

A test of the rebuild replays the run. It makes the same writes to the rebuild's
planning state before the same Done presses, and the rebuild makes the same
calls with the same bounds and results and reaches the same state. Without the
writes the rebuild parts from the original at roll 477. No family-4 gang sees a
hostile human gang, so the family-4 attack draws and its Control after repeated
moves are not compared.

## Conclusion

The run agrees with RULE-AI-023 and RULE-AI-027 for the branches it reaches.
