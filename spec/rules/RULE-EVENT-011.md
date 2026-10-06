---
id: RULE-EVENT-011
title: A Hire refused because the player has the most gangs allowed is reported to its player
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004, EXP-TURN-097, EXP-TURN-098]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When a hire fails because the player has no free roster slot, the player gets a report of it.

## When it runs

As the handler of `HireRosterFull`, at once, during `resolution`.

## Parameters

- `player`: the player whose hire failed.
- `definition`: the gang definition offered.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 8, definition, 0, 0)
```

## Outputs

No return value. Records one type-8 report for `player`.

## Edge cases

- EXP-TURN-097 and EXP-TURN-098 record the report in the original: a hire
  with 80 gangs gives its player one type-8 report whose `arg1` is the
  offered definition and whose `arg2` and `arg3` are 0.

## What the sources say

SRC-MANUAL-GOG does not mention this report.

## Differences between builds

None known.

## Open questions

None known.
