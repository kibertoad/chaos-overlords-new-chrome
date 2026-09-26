---
id: RULE-CITY-003
title: The six players get the six fixed headquarters sectors in a random order, and each headquarters' first site becomes the headquarters site
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CITY-003, FND-UI-033, FND-RNG-005, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-002, FMT-STATE-004]
---

## Summary

Sectors 9, 12, 30, 33, 51 and 54 are the only places a player can start. Each
player is given one of them at random, owns it from the start, and its first
site slot becomes the headquarters.

## When it runs

Once per new match, inside RULE-SETUP-004, after RULE-CITY-002.

## Parameters

None.

## Inputs

`hq_sectors`, and the state of `rng` through `roll`.

## Procedure

```text
let perm: INT32[] = []
for each player in turn_order:
    let v = roll(6) - 1
    let taken = true
    while taken:
        taken = false
        for each u in perm:
            if u == v:
                taken = true
        if taken:
            v = roll(6) - 1
    append(perm, v)

let assigned: INT32[] = []
for each player in turn_order:
    let s = hq_sectors[perm[player]]
    sectors[s].owner = player
    sectors[s].sites[0].definition = 21
    append(assigned, s)
return assigned
```

## Outputs

Returns `assigned: INT32[]`, each player's headquarters sector by player
slot. Sets `owner` and the first site's `definition` of the six headquarters
sectors. Makes one `roll(6)` for every value tried, including repeats: at
least six, and on average 14.7.

## Edge cases

The last player's value is decided by the first five, but the draws for it
still happen until the one free value comes up.

## What the sources say

SRC-MANUAL-GOG, page 14, says each Overlord in Siege starts in a controlled
sector that is important to that player, marked with two gray pylons; it
gives no positions.

## Differences between builds

None known.

## Open questions

- Whether the permutation holds the drawn values 1 to 6 and indexes the table
  with the value minus one, as written here, or stores the zero-based value,
  is not recorded; the result is the same.
- What the headquarters slot's `progress` is set to is not recorded.
