---
id: EXP-TURN-109
title: Does a family-7 gang whose focus names its own best research sector research there while a Research site in it is unfinished?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-109.json
---

## Question

FND-AI-078 reads family 7's focus test at `0x004372E5`: a focus equal to the
best research sector goes straight to item Research, before the handler
tests whether the gang stands in that sector and whether a Research site
there is unfinished. The recorded runs reach the test only with the gang
outside its best sector. When the gang stands in its best sector, its focus
names that sector and a Research site there is unfinished, does it research
rather than influence the site?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Kill 'Em All (`--scenario 4`), Mentality 1
(`--mentality 1`), `--seed 21` and eleven Done presses (`--end-turns 11`).
The human gives no orders. Before the tenth Done press the probe writes 7
into the `family` of player 3's planning record in roster slot 5
(FMT-STATE-007, `--families 10:3:5:7`), as EXP-TURN-040 does.

A family-5 gang that influences a Support site keeps its own sector in the
focus (RULE-AI-024, FND-AI-076). The seed and the record were found in the
rebuild, which played Kill 'Em All, Power and Big 40 at Criminal for seeds 1
to 24 and listed, at each planning entry, the computer gangs whose focus
named the sector they stood in, a sector their player controlled that was its
best research sector and held an unfinished Research site; it then wrote
family 7 into each such record and kept the first write whose planning pass
took the focus test there.

## Observations

The run made 2567 calls of `roll` over eleven Done presses. At the end
player 3's gang in roster slot 5 stands in sector 12, which player 3
controls (FMT-STATE-001, FMT-STATE-002). Its planning record holds family 7,
an older Influence (9), a previous Research (11) of item 13, the same item
number in the focus, and a planned Influence on site slot 2 of sector 12
(FMT-STATE-007): the tenth pass researched, and the eleventh, whose focus
held the item number instead of the sector, influenced the site.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches
the same state and planning records. In the replay, at the planning pass of
turn 10 the gang stands in sector 12, its focus names sector 12, sector 12
is the best research sector the handler finds, and one of its Research sites
is unfinished. With the focus test skipped in that case, so that the gang
influences the unfinished site, the replay parts from the original at call
2146, a roll of the dice whose bound differs.

## Conclusion

The run agrees with RULE-AI-026 and FND-AI-078: the focus test comes before
the Influence of the best sector's sites, so a gang whose focus names the
sector it stands in researches there even while a Research site in it is
unfinished.
