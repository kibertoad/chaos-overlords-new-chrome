---
id: EXP-TURN-044
title: What numbers does the Financial panel draw for the orders and hires of a planning turn?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11, staged original executable and SMACKW32.DLL with junctions to original assets, windowed under the debugging probe, seed 1
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-044.json
---

## Question

Given the human's queued orders and hires, which nine numbers does the
Financial panel draw in its City variant and in its Sector variant?

## Setup

As EXP-TURN-031: seed 1, the setup screen's defaults, a hash-verified
BLD-GOG-EN-1.1. The human's gang starts in sector 51.

## Procedure

Give EXP-TURN-031's orders and hire for turns 1 to 9, then in turn 10 a
Chaos order for roster slot 0 and a Move of slot 1 to sector 52, and press
Done ten times:

`--seed 1 --end-turns 10 --orders 1:0:5:24:0:0,2:1:5:24:0:0,3:0:2:0:0:0,4:0:2:0:0:0,5:0:2:0:0:0,6:1:5:40:0:0,7:1:5:0:0:0,8:0:5:0:0:0,8:1:12:2:0:0,9:0:12:2:0:0,9:1:5:24:0:0,10:0:3:0:0:0,10:1:10:52:0:0 --hires 1:0:51`

Before each Done press, after the turn's orders and hires are written, open
the City variant and the Sector variant of sector 51; in turn 10 open the
Sector variant of sector 52 as well:

`--finance 1:-1,1:51,2:-1,2:51,3:-1,3:51,4:-1,4:51,5:-1,5:51,6:-1,6:51,7:-1,7:51,8:-1,8:51,9:-1,9:51,10:-1,10:51,10:52`

The probe presses the upper part of the Financial control
for the City variant and the lower part for the Sector variant after writing
the sector into the map selection, keeps the nine numbers the panel draws
(FND-FINANCE-003) and the sector the panel function was passed, and presses
the close control. Record every random call and extract numeric state.

## Observations

The run made 2320 calls of `roll`. Every press of the upper part passed -1
to the panel function and every press of the lower part the selected sector.
Each panel drew nine numbers, kept in the fixture under `finance`. In turns 1
to 9 both gangs stand in sector 51, which the player owns, and each Sector
panel drew the same numbers as the City panel. In turn 10 the City panel drew
upkeep -1 for two gangs and a Chaos estimate of 5; the Sector panel of 51
drew upkeep 0 for one gang, the mover's Upkeep given back, and the Sector
panel of 52 upkeep -1 for the one gang moving in.

## Results

A test of the rebuild replays the run. The rebuild makes the same calls with the
same bounds and results and reaches the same state, and before each Done press
its projection of each opened panel gives the nine numbers the original drew.

## Conclusion

The run agrees with RULE-FINANCE-001 for Gang Upkeep and its count, New
Recruits, Equipment for Equip and a one-item Sell, City Officials, Sector Tax
and an owned sector's Chaos estimate, in both variants, and for a Move out of
and into the panel's sector. It shows that the lower part of the Financial
control opens the Sector variant, as FND-FINANCE-002 infers. Site
Protection is 0 throughout; Terminate, the halved Chaos estimate, the
Factory price and a Sell of several items are not compared.
