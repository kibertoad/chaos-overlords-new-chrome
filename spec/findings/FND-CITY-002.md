---
id: FND-CITY-002
title: Each sector's three sites are drawn uniformly and rejected for duplicates and unbalanced modifiers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004764B6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476516
tool: Ghidra 12.1.3
environment: null
---

## Observation

The site proposal function at `0x004764B6` calls the bounded random wrapper
`0x0045D227` for a value from 1 to 21 and subtracts one, giving a site
definition number from 0 to 20. When the scenario global `0x004ABBE8` is 9,
it rejects definitions 4 and 8 and draws again before returning; no other
scenario rejects anything there.

The generator fills a sector's three site slots in order. Slot 0 takes the
first proposal without further tests. Slot 1 rejects a proposal equal to slot
0's definition, then tests the partial combination. Slot 2 compares the
proposal with slot 0 and then with slot 1, then tests the combination. Every
rejected proposal is followed by a new proposal, with a new draw.

The combination test at `0x00476516` sums each of the 14 statistic modifiers
of the site definitions in the slots filled so far and rejects the
combination when any of the 14 sums is below -6 or above +6.

## Interpretation

Each proposal is uniform over the definitions allowed in the scenario, and
there is no weighting by frequency or class. The duplicate and balance tests
condition which definitions slots 1 and 2 end up with. Scenario 9 is
Armageddon, and the two definitions it leaves out are the sites whose benefit
is research, which that scenario already starts with completed.

## Alternatives

The observation does not say whether the generator fills the sectors in
ascending sector order, nor what it writes to each slot's influence progress.
Whether a combination rejected by the balance test discards only the last
proposal (keeping the earlier slots) is implied by the per-slot description
and has not been stated at instruction level.

## How to reproduce

Open `0x004764B6` and find the call to `0x0045D227` with 21, the subtraction
of one, and the comparison of `0x004ABBE8` with 9 followed by the tests for 4
and 8. Follow its callers in the city generator `0x00475FE1` to the per-slot
duplicate comparisons and the call to `0x00476516`, which loops over the 14
modifiers and compares each sum with -6 and 6.
