---
id: FND-CITY-001
title: City generation builds a 32-by-32 density field and derives each sector's Income and starting Tolerance from it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475FE1
tool: Ghidra 12.1.3
environment: null
---

## Observation

The city generator at `0x00475FE1` builds a 32-by-32 field of integers. It
makes 40 pairs of calls to the bounded random wrapper `0x0045D227` with the
argument 32, each giving a value from 1 to 32, and subtracts one from each to
get a zero-based centre: the first call of a pair gives the column, the second
the row. For each centre it runs four passes of growing radius that add to the
cells of clipped square footprints 2 by 2, 4 by 4 and 6 by 6 around the centre,
and it caps every cell at 4.

Each of the 64 sectors then sums the 4-by-4 block of cells that corresponds to
it (the city's 8-by-8 sectors cover the 32-by-32 field). The sector's Income
byte is the block's average, rounded, plus 3. Its starting Tolerance byte is
17 minus that Income.

## Interpretation

A new city's sector Income runs from 3 to 7 and its starting Tolerance from 10
to 14. Income comes from the density field alone, not from the three sites'
Cash, and dense areas of the field have high Income and low Tolerance.

## Alternatives

The observation does not give the exact cell bounds of each footprint relative
to the centre, what the radius-0 pass adds, or the arithmetic of the rounding
(round half up, or another rule). A reading in which the pass of radius `r`
covers the cells from `centre - r` up to but not including `centre + r` in
each direction, so that radius 0 adds nothing, fits the three footprint sizes
given, but the instruction-level bounds have not been written down. Which bytes
of the sector record the Income and Tolerance are written to is not recorded.

## How to reproduce

Open `0x00475FE1` in Ghidra. It is the function the fresh-game initializer
`0x0046DC10` calls for the city (see FND-RNG-005). Find the loop of 40
iterations that calls `0x0045D227` twice with 32, the nested footprint loops
with the cap at 4, and the per-sector loop that sums a 4-by-4 block, adds 3 and
stores 17 minus the result.
