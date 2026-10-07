---
id: EXP-TURN-064
title: Does Chaos in a sector under police presence crack down again and pay, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-064.json
---

## Question

No other recorded run rolls Chaos in a sector under police presence. When
three of the human's gangs keep doing Chaos in their headquarters sector until
the police arrive, does a further Crackdown there and a payout there follow
RULE-CHAOS-001 and RULE-CHAOS-002?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Greed (`--scenario 0`), Mentality 0 (`--mentality 0`),
seven Done presses (`--end-turns 7`) and `--seed 9`. Before the Done press of
turn 1, write Chaos (3) into the `action` and `repeat_action` bytes of the
human's gang in roster slot 0, with 0 in `target` and `target_2`; before turn
2 do the same for roster slot 1 and before turn 3 for roster slot 2
(`--orders 1:0:3:0:0:1,2:1:3:0:0:1,3:2:3:0:0:1`). In turns 1 and 2 write a
hire order for offer slot 0 into sector 51, the headquarters sector
(`--hires 1:0:51,2:0:51`), so the gangs of roster slots 1 and 2 arrive there.

## Observations

The run made 1360 calls of `roll` over seven Done presses. At the end
`elapsed_turns` is 7 and every player is still active. Sector 51 is neutral
and holds `crackdown_turns` 5. The human's cash is -27 and its scenario score
-22.

## Results

A test of the rebuild replays the run with DEV-AI-007 switched off. The rebuild
makes the same calls with the same bounds and results and reaches the same
state. In the replay, sector 51 cracks down in turn 6 while it is under police
presence, and in turn 7 its Chaos stays at or below the Tolerance and the human
is paid for it, as in a sector without presence.

An eighth Done press eliminates the human in the original and in the rebuild,
after 1558 calls of `roll`, so the run stops at seven presses and its end
state is read.

## Conclusion

The run agrees with RULE-CHAOS-001 and RULE-CHAOS-002 in a sector under
police presence: no test of presence is made, so the sector cracks down again
and, when it does not, pays.
