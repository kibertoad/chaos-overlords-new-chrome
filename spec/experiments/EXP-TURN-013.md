---
id: EXP-TURN-013
title: Do twenty-five turns of a new local Eliminate game draw and resolve as the spec gives?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-013.json
---

## Question

As EXP-TURN-010, in Eliminate (scenario 7), where the computer players' gangs
take families 10, 11 and 12: do the turns make the draws and reach the state
the spec gives?

## Setup

As EXP-TURN-001.

## Procedure

1. As EXP-TURN-010, with Eliminate chosen on the setup screen
   (`--scenario 7`), pressing Done twenty-five times, with Hide (8) in
   `action` and `repeat_action` of the human's gang in roster slot 0 before
   the first press (`--orders 1:0:8:0:0:1`).
2. Repeat the run with its seed (`--seed 46976`), noting every call of the
   sector selector `0x00408642` (`--trace-calls 0x00408642`) over twenty-four
   turns and copying the writable sections at the entry of call 8224 of
   `roll`, counting from 0.
3. Repeat it with the same seed, copying the writable sections at the entry of
   call 655 over five turns, and at the entries of calls 2360 (twelve turns),
   5306 (eighteen), 7394 (twenty-two), 7404 (twenty-two) and 8220
   (twenty-three), one repetition each.

## Observations

The run made 10974 calls of `roll`, 315 before the first Done press. The copy
held 25 in `elapsed_turns`, every player active, and the human's gang in
roster slot 0 in sector 30 with Force 10.

In the trace, player 1's first two selector calls of turn 5 were the mode-9
call from `0x0042A9BC`, which made call 654, and the call from `0x00435A86`
with mode 73 and slot 1, which returned 9 with no call of `roll`. At call 655,
player 1's gang in roster slot 1 stood in sector 0 and the gangs in slots 0,
2, 3 and 4 in sector 9; the first pair of `selector_pairs` held score 4 with
sector 9, and the table element of sector 9 held 4.

The copies at calls 2360, 5306 and 7394 had every gang where the rebuild has
it at the same point. At call 7404, after the planning of turn 22, player 2's
first gang, in sector 54, and player 5's, in sector 26, had Move planned to
sectors 61 and 25, which the mode-9 calls had returned; at call 8220 they
stood there. In the copy at call 7404 the site slots of the four sectors,
as (definition, progress), were:

| Sector | Owner | Slot 0 | Slot 1 | Slot 2 |
|---|---|---|---|---|
| 54 | 2 | finished, Stealth 0 | finished, Stealth 1 | finished, Stealth 0 |
| 61 | 2 | finished, Stealth 1 | unfinished | unfinished |
| 26 | 5 | finished, Stealth 1 | finished, Stealth 0 | unfinished |
| 25 | 5 | finished, Stealth 1 | unfinished | unfinished |

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results and reaches the same generator position and state.

A rebuild that passed the acting gang's sector in family 12's encoded mode made
a call of `roll(258)` for player 1's slot 1 in turn 5, where the original made
none: the original's mode names the sector of roster slot 0 (FND-AI-070). With
that corrected, a rebuild that compared the summed positive Stealth of the
finished sites for family 10 kept both first gangs in place in turn 22, where
the original moved them: the value selector 8 compares is the Stealth of the
last finished site (FND-AI-071), 0 in sectors 54 and 26 and 1 in sectors 61
and 25.

## Conclusion

The run agrees with the spec over twenty-five turns of Eliminate, with
RULE-AI-030 and RULE-AI-028 as FND-AI-070 and FND-AI-071 correct them.
