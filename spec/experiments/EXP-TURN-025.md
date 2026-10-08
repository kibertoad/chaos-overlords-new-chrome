---
id: EXP-TURN-025
title: Does a family-6 gang that fails its strength test buy equipment, in Kill 'Em All at Homicidal Maniac, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-025.json
---

## Question

A family-6 gang that sees another player's gang draws an attack target once.
When the strength test fails it buys a better weapon or armor if it can, and
otherwise heals or attacks the last of up to five more draws (RULE-AI-025).
No earlier run reaches a failed family-6 draw. Over 22 turns of Kill 'Em All at
Homicidal Maniac with an idle human, does the family-6 handler play out as the spec
gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 23758`, Kill 'Em All (`--scenario 4`), Mentality
3 (`--mentality 3`) and twenty-two Done presses (`--end-turns 22`), with no
orders.

The seed was found in the rebuild, which played Power, Acceptance, Dominance,
Kill 'Em All, Big 40 and Armageddon at Criminal, Crime Lord and Homicidal
Maniac from a range of seeds with the human idle, and kept the matches in
which a family-6 gang planned Equip before any computer player's Move was
refused (DEV-AI-007).

## Observations

The seed was 23758. The run made 11202 calls of `roll`, 321 before the
first Done press.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results and reaches the same generator position and state. In
the replay player 1's family-6 gang in roster slot 19 fails its first strength
test in turn 22 and plans Equip.

## Conclusion

The run agrees with the family-6 branches of RULE-AI-025 it reaches.
