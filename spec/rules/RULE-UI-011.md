---
id: RULE-UI-011
title: The sector values on the main console
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-035, FND-UPKEEP-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-002]
---

## Summary

For the selected sector the console shows Income and Tolerance to everyone, and
Support and Cash only to the sector's owner; other players see 0 there.

## When it runs

When the city screen or the detailed sector screen draws the selected sector's
values.

## Parameters

- `sector_number` (`INT32`): the selected sector.

## Inputs

`sectors`, `active_player`.

## Procedure

```text
let record = sectors[sector_number]
let values: INT32[4] = [record.income, record.tolerance, 0, 0]
if record.owner == active_player:
    values[2] = record.support
    values[3] = record.cash_yield
return values
```

## Outputs

Returns the Income, Tolerance, Support and Cash values in that order.

## Edge cases

A neutral sector's owner is -1, which is never the active player, so its Support
and Cash show 0.

## What the sources say

SRC-MANUAL-GOG, pages 17 and 18 (Game Info Screen), describes the sector
values shown at the top of the control panel for the selected sector, among
them its Income, Tolerance and Support. How it defines Income has not been
compared closely with the two fields of FMT-STATE-002.

## Differences between builds

None known.

## Open questions

- Whether Income and Tolerance are also hidden from non-owners.
- The `income` field of FMT-STATE-002 is disputed; this rule follows
  FND-UI-035.
- Which number helper draws the values.
