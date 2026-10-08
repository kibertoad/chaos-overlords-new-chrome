---
id: EXP-TURN-023
title: Do family-1 gangs snitch, and does a Goon computer player's Influence advance a site, as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-023.json
---

## Question

Family 1 is given by hire role 1 in Power, Kill 'Em All, Big 40 and
Armageddon (RULE-AI-002), and its Snitch and crime branches need a sector
whose owner reads as human and more than 50 cash (RULE-AI-020). No earlier run
reaches them. At Mentality Goon the computer players are in difficulty band 0,
which removes a fifth of their Influence pool (RULE-AI-018,
RULE-INFLUENCE-001). Over thirty turns of Power at Goon with an idle human, do
the family-1 gangs and the band-0 Influences play out as the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with `--seed 3027`, Power (`--scenario 1`), Mentality 0
(`--mentality 0`), one year (`--turns 52`) and thirty Done presses
(`--end-turns 30`), with no orders.

The seed was found in the rebuild, which played Power at every Mentality from
a range of seeds with the human idle and noted which family branches the
computer players reached.

The same run was made three more times with `--end-turns 7` and
`--dump-at-roll` 835, 845 and 1030, which copy the data sections when that
call of `roll` returns, and once with `--end-turns 8` and
`--trace-calls 0x00475F70`, which notes the arguments and result of every call
of the dice helper.

## Observations

The seed was 3027. The run made 12770 calls of `roll`, 308 before the first
Done press. In turn 6 the helper was called once from the band-0 Influence
call `0x00472DC3` with 8 dice and threshold 5, after 835 calls of `roll`,
and returned 4 successes. Sector 51, owned by player 2, had sites of
definitions 21, 4 and 1. In the copy after call 835 the progress of the second
site was 0; in the copies after calls 845 and 1030 it was 12, the Resistance
of definition 4. No other Influence reached sector 51 before turn 7. In turn
7 player 2's gang in roster slot 4, family 7 with previous action Influence,
had Research (11) of item 13 in its record.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results and reaches the same generator position and state. In
the replay, family-1 gangs plan Snitch after Control and then again after
Snitch, and Chaos after Snitch; others heal, take sectors by Control, equip and
move.

## Conclusion

The band-0 Influence set the site's progress to its pool of 8 plus its 4
successes, completing a site of Resistance 12 in one turn, where
RULE-INFLUENCE-001 gave progress 4 (FND-INFLUENCE-004, BUG-INFLUENCE-001).
With that corrected the run agrees with the family-1 branches of RULE-AI-020
it reaches.
