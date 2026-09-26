---
id: RULE-RESEARCH-002
title: A new match starts each player with each item's research difficulty, or with every item researched in Armageddon
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-RESEARCH-002, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-DATA-003]
---

## Summary

At the start of a match every player needs the full research difficulty of
each item, so only items with difficulty 0 are available at once. In the
Armageddon scenario every item starts researched.

## When it runs

Once, when a new match is set up, after `scenario` is chosen.

## Parameters

None.

## Inputs

`scenario`, and the `research_difficulty` of each of the 64 entries of
`item_definitions`.

## Procedure

```text
for item in 0..64:
    for player in 0..6:
        if scenario == 9:
            research_remaining[item * 6 + player] = 0
        else:
            research_remaining[item * 6 + player] = UINT8(item_definitions[item].research_difficulty)
```

## Outputs

No return value. Writes all 384 elements of `research_remaining`. Makes no
random draw.

## Edge cases

- Only the low byte of each item's research difficulty is kept.
- The loop covers every item record, including the padding records of the
  item table, and every player slot, including empty ones.

## What the sources say

SRC-MANUAL-GOG, page 14, describes Armageddon as a scenario in which every
Overlord has all items available from the start. The manual does not say
that items of difficulty 0 start researched.

## Differences between builds

None known.

## Open questions

- Whether the original writes the Armageddon zeros in the same item-major
  order is not recorded; the order does not change the result.
