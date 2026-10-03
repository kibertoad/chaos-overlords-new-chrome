---
id: EXP-TURN-043
title: Does the Move-capacity repair send back a computer player's mover into a sector that would hold seven of its gangs?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11, staged original executable and SMACKW32.DLL with junctions to original assets, windowed under the debugging probe, seed 18
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-043.json
---

## Question

When a computer player's Moves would put seven of its gangs in one sector,
does the original send the mover back to the sector it starts from, as the
first choice of RULE-MOVE-002 does?

## Setup

As EXP-TURN-001, with scenario 4 (Kill 'Em All), Mentality 2, a 52-turn
limit and seed 18. The original executable is hash-verified against
BLD-GOG-EN-1.1.

## Procedure

Run the probe with `--scenario 4 --mentality 2 --turns 52 --seed 18
--end-turns 24`. The human gives no orders and presses Done 24 times. Record
every random call and extract numeric state at the 25th planning entry.

The seed was found in the rebuild, which played this setup and scenarios 0
and 1 for seeds 1 to 60 with an idle human for 52 turns and counted each
repair of RULE-MOVE-002. Seed 18 gave the earliest repair in Kill 'Em All,
in the resolution of turn 24. The rebuild reached the
fallback that draws a random neighbour (RULE-AI-007) in none of those
runs, nor in all ten scenarios for seeds 61 to 100.

## Observations

The run made 11416 calls of `roll`, with the Done presses at the counts
listed in the fixture. At the 25th planning entry player 2 holds six gangs
in sector 62 and two in sector 54. The recording holds no orders of turn 24,
so it does not show which gang was ordered to move.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches
the same state. In the rebuild's resolution of turn 24, player 2's Moves
would put seven of its gangs in sector 62. The mover comes from sector 54,
which counts fewer than six, so the repair's first choice takes it and
rewrites its destination to sector 54, its own sector, and every gang's
sector then agrees with the original's.

## Conclusion

The run agrees with RULE-MOVE-002 for a computer player whose mover comes
from a sector counting fewer than six. It makes no draw in the repair, so
the fallback of RULE-AI-007 is not compared.
