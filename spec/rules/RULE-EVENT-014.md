---
id: RULE-EVENT-014
title: An Equip that fails for lack of cash is reported to its player
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When an Equip cannot be paid for, the player gets a report of it.

## When it runs

As the handler of `EquipCashShort`, at once, during `resolution`.

## Parameters

- `player`: the player whose Equip failed.
- `sector`: the sector of the gang whose Equip failed.
- `definition`: that gang's `definition` byte (FMT-STATE-001).

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 6, 2, sector, definition)
```

## Outputs

No return value. Records one type-6 report for `player`. The cash failures share type 6 and tell Bribe, Equip and Hire apart by an argument of 1, 2 or 4; this one passes 2 as its first argument. The report does not name the item.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG does not mention this report.

## Differences between builds

None known.

## Open questions

- The third argument is the gang's byte at FMT-STATE-001 offset `0x01`, which
  that format places as `definition` from an outside source only.
