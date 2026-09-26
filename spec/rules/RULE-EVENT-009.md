---
id: RULE-EVENT-009
title: A Hire that fails for lack of cash is reported to its player
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When a hire cannot be paid for, the player gets a report of it.

## When it runs

As the handler of `HireCashShort`, at once, during `resolution`.

## Parameters

- `player`: the player whose hire failed.
- `definition`: the gang definition offered.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 6, 4, definition, 0)
```

## Outputs

No return value. Records one type-6 report for `player`. The cash failures share type 6 and tell Bribe, Equip and Hire apart by an argument of 1, 2 or 4; this one passes 4 as its first argument, and the gang definition as its second.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG does not mention this report.

## Differences between builds

None known.

## Open questions

None known.
