---
id: RULE-EVENT-013
title: Losing control of a sector is reported to the previous owner
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When a player loses a sector, to another player's Control or to a third Crackdown within five turns, that player gets a report of it.

## When it runs

As the handler of `ControlLostReport`, at once, during `resolution`.

## Parameters

- `player`: the player who lost the sector.
- `sector`: the sector lost.
- `taker`: the player whose Control took the sector, or 0 when a third
  Crackdown took it.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 3, sector, taker, 0)
```

## Outputs

No return value. Records one type-3 report for `player`. RULE-CONTROL-001 and RULE-POLICE-002 emit the event.

## Edge cases

- When a sector with no owner reaches a third Crackdown, RULE-POLICE-002 emits
  the event with `player` -1 and RULE-EVENT-002 records nothing.
- The police case passes 0 as `taker`, which is also a player slot; the panel
  never reads this argument (FMT-STATE-006).

## What the sources say

SRC-MANUAL-GOG does not list losing a sector among the events the Last Turn Events panel shows.

## Differences between builds

None known.

## Open questions

None known.
