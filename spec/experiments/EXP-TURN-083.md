---
id: EXP-TURN-083
title: Does a family-7 computer gang in a hostile human's sector draw only human gangs, and does a computer's Influence outside its own sectors resolve?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-083.json
---

## Question

No other recorded run has a family-7 gang draw an attack target where its
weight is 10 and the player is hostile toward the sector's owner. Does it draw
from the human players' gangs only, as RULE-AI-026 gives? In the same turn a
family-7 gang of player 2 plans Influence on a site of neutral sector 10. Does
the resolver roll it, as RULE-INFLUENCE-001 gives with no owner test, and does
the rebuild before planning count the completed site, as RULE-SITE-001 gives?

## Setup

As EXP-TURN-001.

## Procedure

As EXP-TURN-004 with Armageddon (`--scenario 9`), Mentality 3 (`--mentality
3`), eight Done presses (`--end-turns 8`) and `--seed 9`. The human gives no
orders. Before the eighth Done press the probe writes 7 into the `family` of
the planning records in every roster slot of the computer players that held a
gang at the time (FMT-STATE-007, `--families
8:1:0:7,8:1:1:7,8:1:2:7,8:1:3:7,8:1:4:7,8:1:5:7,8:1:6:7,8:2:0:7,8:2:1:7,8:2:2:7,8:2:3:7,8:2:4:7,8:2:5:7,8:2:6:7,8:3:0:7,8:3:1:7,8:3:2:7,8:3:3:7,8:3:4:7,8:3:5:7,8:3:6:7,8:3:7:7,8:4:0:7,8:4:1:7,8:4:2:7,8:4:3:7,8:4:4:7,8:4:5:7,8:4:6:7,8:5:0:7,8:5:1:7,8:5:2:7,8:5:3:7,8:5:4:7,8:5:5:7`),
as EXP-TURN-040 does.

The seed was found in the rebuild, which played every scenario at every
Mentality for seeds 1 to 12, with families 0, 4, 5 and 7 written into every
computer gang at turn 3, 8 or 14 and with no write, and recorded which of a
list of unreached planner branches each match took.

## Observations

The run made 1435 calls of `roll` over eight Done presses. At the end
`elapsed_turns` is 8 and no player's `controller` has changed. At the end
player 2's gang in roster slot 1 stands in sector 10, its Force plus
effective Influence making a pool of 10 (FMT-STATE-001, FMT-DATA-002), its
planning record holds a planned Influence on site slot 2 (FMT-STATE-007),
sector 10 is neutral, and that site is complete: the sector record's
Tolerance and Support hold that site's contributions (FMT-STATE-002,
FMT-DATA-001). The eighth turn rolls 75 dice at `0x00475FBB` before the hire
draws: two Influence pools of 10, one of 13, the Heal pools and the 27 of the
attack on the human gang.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run with
DEV-AI-007 switched off and writes the same families before the same Done
press. The rebuild makes the same calls up to call 1420, the family-7 draws
from the human-only pool included. A computer's planned Influence in a sector
it does not control gives the rebuild no command (DEV-AI-002), so it does not
roll the ten dice of the Influence in sector 10 and reaches the combat dice
ten calls early. The test holds the run as a known divergence at that call.

## Conclusion

The run agrees with RULE-AI-026 for the human-only pool. It shows the original
resolving a computer's Influence in a neutral sector, with a pool of the
gang's Force plus Influence, and the record rebuild counting the site that
Influence completed in a sector nobody owns (RULE-INFLUENCE-001,
RULE-SITE-001).
