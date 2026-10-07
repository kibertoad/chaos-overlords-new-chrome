---
id: EXP-TURN-029
title: Does an armed gang strike back at a bare-handed Martial Artist?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-029.json
---

## Question

RULE-ATTACK-001 gives no retaliation when the attacker fights bare handed with
Martial Arts above 0 and the target is not also a bare-handed Martial Artist.
The attacks in earlier runs are made by gangs without Martial Arts. Does the
original leave out the retaliation?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 369` and ten Done presses (`--end-turns 10`).
Before the first Done press the human's gang in roster slot 0 is set to Hide
with Hide as its recurring order, and the probe writes offer slot 0's hire
order with sector 9, the gang's sector. Before the second, the hired gang in
roster slot 1 is given the same recurring Hide. Before the tenth, it is given
Attack on player 5's gang in roster slot 2 with no recurring order
(`--orders 1:0:8:0:0:1,2:1:8:0:0:1,10:1:1:5:2:0 --hires 1:0:9`).

The seed was found in the rebuild, which played this setup for seeds 1 to 400
and gave the Attack in the first turn in which a visible computer gang that is
not hiding and is armed or has no Martial Arts stood in the hired gang's
sector. Seed 369 reaches it earliest with no computer player given a Move that
DEV-AI-007 refuses.

## Observations

The run made 2572 calls of `roll`. At the end the hired gang, of definition
71, was in sector 9 with Force 5, no weapon, Combat 3, Defense 4 and Martial
Arts 3. Player 5's gang in slot 2, of definition 66, held weapon 12 and had
Martial Arts 0; it had moved on to sector 16.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results and reaches the same generator position and state. In
the rebuild's replay the attack rolls 5 dice for one point of damage against a
target of Force 8 and Combat 2, and no retaliation dice. A retaliation would
have rolled 8 + 2 - 4 = 6 dice, so the original, which makes the same calls,
made no retaliation roll either.

## Conclusion

The run agrees with RULE-ATTACK-001: an armed target does not strike back at a
bare-handed Martial Artist.
