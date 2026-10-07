---
id: EXP-TURN-104
title: Does a Research gang that acts after a site of its sector is completed in the same instant phase roll without the site's Research?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-104.json
---
## Question

The gangs' effective statistics are rebuilt at `turn_start` (RULE-GANG-001),
so a site completed during the instant phase adds nothing to a gang acting
later in the same phase (RULE-TURN-003, RULE-RESEARCH-001, FND-GANG-001). No
earlier run has a Research gang act after a site with a Research value is
completed in its sector in the same phase. Does the original roll the
Research pool without the site?

## Setup

As EXP-TURN-001, with scenario 0 (Greed), Mentality 0, a two-year limit
(`turn_limit` 104) and seed 112.

## Procedure

Run the probe with `--scenario 0 --mentality 0 --turns 104 --seed 112
--end-turns 3 --cash 1-3:0:30000 --hires 1:0:33
--orders 1:0:9:1:0:1,3:1:11:31:0:0`. In turn 1 the human's gang in roster
slot 0 is given a recurring Influence (9) on site slot 1 of its starting
sector 33, which the human owns, and offer slot 0 is hired into sector 33
(RULE-HIRE-003), with 30000 written into the human's `cash` before every Done
press (FND-AI-055). The site's definition gives a Research value above 0
(FMT-DATA-001), which the gangs of the sector's owner take once it is
complete (RULE-GANG-001). In turn 3 the hired gang, now in roster slot 1, is given
Research (11) of item 31. Record every random call and extract numeric state
at the fourth planning entry.

The seed and plan were found in the rebuild, which played it for the seeds
whose starting sector holds a site with a Research value and found the turn
in which the slot 0 gang's Influence completes the site; the Research order is
given in that turn only, so the draws before it do not change.

## Observations

The run made 606 calls of `roll`, with the Done presses at the counts listed
in the fixture.

## Results

A test of the rebuild replays the run, with
the same cash written before each Done press. The rebuild makes the same calls
with the same bounds and results and reaches the same state. In its replay
the slot 0 gang's Influence of turn 3 completes the site, and the slot 1 gang
then rolls 5 dice for Research: its Force plus the Research rebuilt at the
start of the turn, before the site was complete. Counting the site would have
added its Research value in dice and as many calls of `roll`. At the next
`turn_start` the gang's Research is rebuilt with the site's value added.

## Conclusion

The run agrees with RULE-RESEARCH-001 and RULE-TURN-003: a site completed
earlier in the instant phase adds nothing to a later gang's pool in that
phase.
