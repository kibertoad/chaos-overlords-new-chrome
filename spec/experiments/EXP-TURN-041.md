---
id: EXP-TURN-041
title: Does a 26-turn Greed match reach the same final city state before awards?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11, staged original executable and SMACKW32.DLL with junctions to original assets, windowed under the debugging probe, seed 52421
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-041.json
---

## Question

Does the same new-game seed and idle human input reach the same random stream
and final city state when a six-month Greed match ends?

## Setup

As EXP-TURN-001, using scenario 0, 26 turns and seed 52421. The original
executable is hash-verified against BLD-GOG-EN-1.1. The probe's staged
installation and final-view capture are described by FND-UI-041.

## Procedure

Press Done 26 times, dismissing result panels. Stop at the surviving human's
final city view before Done advances to the awards controller. Record every
random call and the writable state, and extract only numeric fixture rows.
Omit the player award rows: their backing table has not yet been built at
this endpoint (FND-OBJECTIVE-004). Do not infer awards from those stale cells.

## Observations

The original makes 10647 random calls. Its elapsed-turn count is 25, the
match-over flag is set, and it presents the final city view for player 0.
FND-UI-041 records the calendar capture and completion flag.

## Results

The detailed replay comparator checks the same random bounds and results,
generator consumption and state, players, sectors, gangs, reports, statistics,
scenario scores, standings and outcome turn. It does not compare awards at
this endpoint. Post-awards experiments retain their award comparisons.

## Conclusion

This experiment covers one natural final-city endpoint. It does not establish
all scenarios, final-view input, awards presentation or multiplayer end flow.
