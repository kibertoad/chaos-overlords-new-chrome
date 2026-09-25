---
id: RULE-HIRE-004
title: A new match starts with every hire offer vacant and no hire order
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HIRE-001]
conflicting: []
split_with: []
related: []
---

## Summary

A new match starts with no gangs on offer. The three offers of every player
are filled when that player first plans (RULE-HIRE-002).

## When it runs

Once, when a new match is set up, before the first `turn_start`.

## Parameters

None.

## Inputs

None.

## Procedure

```text
for player in 0..6:
    for slot in 0..3:
        hire_offers[player * 3 + slot] = -100
        hire_orders[player * 3 + slot] = -1
```

## Outputs

No return value. Sets all 18 `hire_offers` to -100 and all 18 `hire_orders`
to -1. Makes no random draw.

## Edge cases

Every player slot is written, including empty ones.

## What the sources say

SRC-MANUAL-GOG does not describe the state before the first offers.

## Differences between builds

None known.

## Open questions

- Whether a loaded save goes through this step, and whether the save file
  holds the two arrays, is not recorded.
