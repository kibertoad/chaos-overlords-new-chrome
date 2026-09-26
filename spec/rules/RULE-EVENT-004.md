---
id: RULE-EVENT-004
title: A Crackdown is reported to each player who had a gang in its sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When the police crack down on a sector, each player who had a gang there when
the turn's orders began to be carried out gets a report of it.

## When it runs

As the handler of `CrackdownReport`, which the rule creating a Crackdown in
`chaos_phase` emits once for each player who had a gang in the sector when
`resolution` began.

## Parameters

- `player`: the player slot to report to.
- `sector`: the sector the Crackdown was created in.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 1, sector, 0, 0)
```

## Outputs

No return value. Records one type-1 report for `player`, naming `sector`.
The resolver emits `CrackdownReport` for players 0 to 5 in ascending order,
from the `sector_presence` table it fills at the start of resolution, and all
of a sector's Crackdown reports come before its control-loss report
(RULE-EVENT-013) [FND-EVENT-004].

## Edge cases

A player whose gang left the sector or died during the turn is judged by where
the gang was when resolution began, so still receives the report. A player
whose gang arrives later in the turn does not.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), lists a sector Crackdown among the
events the Last Turn Events panel shows.

## Differences between builds

None known.

## Open questions

None known.
