---
id: EXP-TURN-045
title: Which site markers does the city show for a Search filter of every even site definition?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11, staged original executable and SMACKW32.DLL with junctions to original assets, windowed under the debugging probe, seed 1
starting_state: new-game
recording: null
repetitions: 1
fixture: EXP-TURN-045.json
---

## Question

With some site definitions selected in the human's Search filter, which
markers does the city draw, with which ordinals, and which of them as
controlled?

## Setup

As EXP-TURN-031: seed 1, the setup screen's defaults, a hash-verified
BLD-GOG-EN-1.1. The human starts in sector 51.

## Procedure

Run the probe with `--seed 1 --end-turns 3 --search
1:0+2+4+6+8+10+12+14+16+18+20`. Before the first Done press the probe sets the
human's `search_filters` entries for the eleven even site definitions, as the
Search panel's rows would. The human gives no orders. The probe keeps every
marker of each city redraw (FND-SEARCH-006), and the fixture holds the last
redraw before the dump at the fourth planning entry. Record every random
call and extract numeric state.

## Observations

The run made 612 calls of `roll`. The last redraw was drawn for player 0 and
drew 98 markers, sector by sector. In sector 51 the Headquarters, definition
21, is drawn first as controlled, with ordinal 0, and the selected site after
it with ordinal 1. Every other marker is drawn as not controlled. Where a
sector holds a site of an odd definition between two selected ones, the
second selected site takes the next ordinal.

## Results

`tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs` replays the run. The
rebuild makes the same calls with the same bounds and results and reaches
the same state, and for the same filter its city shows the same 98 markers,
with the same definition, sector, ordinal and controlled flag, in the same
order.

## Conclusion

The run agrees with RULE-SEARCH-002 for controlled sites, for sites a filter
selects and for the ordinals of a sector's drawn sites. No site controlled by
the human other than its Headquarters, and no site in a sector the human
owns without having completed it, is compared.
