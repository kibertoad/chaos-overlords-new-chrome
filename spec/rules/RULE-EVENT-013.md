---
id: RULE-EVENT-013
title: Losing control of a sector is reported to the previous owner
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001]
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

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 3)
```

## Outputs

No return value. Records one type-3 report for `player`. RULE-CONTROL-001 and RULE-POLICE-002 emit the event.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG does not list losing a sector among the events the Last Turn Events panel shows.

## Differences between builds

None known.

## Open questions

- The arguments the report carries are not recorded; the event's own arguments are not shown to be among them.
- Whether the two places that emit the event pass the same arguments is not recorded.
- What is recorded when a neutral sector reaches a third Crackdown, where RULE-POLICE-002 emits the event with the owner -1, is not recorded.
