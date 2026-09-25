---
id: RULE-EVENT-007
title: A completed item is reported to the player whose Research completed it
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When Research completes an item, the researching player gets a report of it.

## When it runs

As the handler of `ResearchCompleted`, at once, during `resolution`.

## Parameters

- `player`: the player whose gang completed the research.
- `item`: the item researched.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 5)
```

## Outputs

No return value. Records one type-5 report for `player`.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), lists completing research on an item among the events the Last Turn Events panel shows.

## Differences between builds

None known.

## Open questions

- The arguments the report carries are not recorded; the event's own arguments are not shown to be among them.
