---
id: RULE-EVENT-010
title: A Hire refused because its sector is full is reported to its player
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When a hired gang cannot be placed because its sector already holds as many of the player's gangs as it can, the player gets a report of it.

## When it runs

As the handler of `HireSectorFull`, at once, during `resolution`.

## Parameters

- `player`: the player whose hire failed.
- `sector`: the sector chosen for the gang.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 7, sector, 0, 0)
```

## Outputs

No return value. Records one type-7 report for `player`.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG does not mention this report.

## Differences between builds

None known.

## Open questions

None known.
