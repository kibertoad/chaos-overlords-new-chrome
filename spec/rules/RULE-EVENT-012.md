---
id: RULE-EVENT-012
title: Taking control of a sector is reported to the new owner
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When a player's Control order takes a sector, that player gets a report of it.

## When it runs

As the handler of `ControlGainedReport`, at once, during `resolution`.

## Parameters

- `player`: the player who took the sector.
- `sector`: the sector taken.
- `previous`: the sector's owner before, or -1 when it had none.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 2, sector, previous, 0)
```

## Outputs

No return value. Records one type-2 report for `player`.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), lists taking over a sector among the events the Last Turn Events panel shows.

## Differences between builds

None known.

## Open questions

None known. The resolver's only type-2 call is in the Control pass, so no other ownership change records one [FND-EVENT-004].
