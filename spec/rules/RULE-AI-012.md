---
id: RULE-AI-012
title: The AI hire destination helper writes an encoded sector directly, and has two random modes nobody reaches
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-016, FND-AI-017]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-001, FMT-STATE-002]
---

## Summary

When a computer player hires, it names the sector the new gang will appear in.
The planner always passes that sector encoded as `sector + 0x40`, and the
helper stores it. Two other modes, which pick a sector at random among the
player's land and gangs, exist in the code but the planner never uses them.

## When it runs

When RULE-AI-010 calls `hire_destination`, at the end of a computer player's
planning pass. The resolver reads the stored destination during `hire_phase`.

## Parameters

None.

## Inputs

`gangs` (`sector`), `sectors` (`owner`), and `rng_state` through `roll`.

## Procedure

```text
define hire_destination(player, mode, offer):
    if mode >= 0x40:
        hire_orders[player * 3 + offer] = mode - 0x40
        return mode - 0x40
    if mode >= 2:
        return 99
    if mode == 0:
        let low = 64
        let high = -1
        for slot in 0..81:
            let s = gangs[player * 81 + slot].sector
            if s != GANG_INACTIVE:
                low = min(low, s)
                high = max(high, s)
        let low_n = 0
        let high_n = 0
        for slot in 0..81:
            let s = gangs[player * 81 + slot].sector
            if s == low:
                low_n = low_n + 1
            if s == high:
                high_n = high_n + 1
        if offer >= 0 and (low_n < 6 or high_n < 6):
            # the pick is overwritten below; only the draw has an effect
            roll(2)
    # mode 1, and mode 0 falling through
    let places = []
    for s in 0..64:
        if sectors[s].owner == player:
            append(places, s)
    for slot in 0..81:
        let s = gangs[player * 81 + slot].sector
        if s != GANG_INACTIVE:
            append(places, s)
    let k = roll(count(places))
    let dest = -1
    if count(places) > 0:
        dest = places[k - 1]
    hire_orders[player * 3 + offer] = dest
    return dest
```

## Outputs

Returns the destination sector, 99 for modes 2 to 63, or -1 when mode 0 or 1
finds no owned sector and no gang. Stores the destination in the offer's
element of `hire_orders`. The encoded path makes no draw; mode 1 makes one
`roll`, and mode 0 makes a `roll(2)` before it when an extreme has room.

## Edge cases

In modes 0 and 1 a sector appears once for being owned and once more for each
gang in it, so crowded sectors are more likely. An empty list still calls
`roll`, whose bound is raised to 1, and the destination is -1.

## What the sources say

SRC-MANUAL-GOG does not describe it.

## Differences between builds

None known.

## Open questions

- Where the helper stores the destination is not recorded; it is taken to be
  the offer's element of `hire_orders`, which holds the placement sector of a
  hire order (FND-HIRE-001).
- In mode 0 the extremes are taken over the player's own gangs; the finding
  says "occupied gang records" without naming the player.
- Modes 0 and 1 are not reached by any recorded call (FND-AI-017).
