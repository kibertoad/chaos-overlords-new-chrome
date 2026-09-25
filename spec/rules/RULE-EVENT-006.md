---
id: RULE-EVENT-006
title: A completed site is reported to the player whose Influence completed it
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EVENT-001, FND-EVENT-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When an Influence completes a site, the influencing player gets a report of it.

## When it runs

As the handler of `SiteCooperationAchieved`, at once, during `resolution`.

## Parameters

- `player`: the player whose gang completed the site.
- `sector`: the site's sector.
- `slot`: the site's slot in the sector, 0 to 2.

## Inputs

None.

## Procedure

```text
call RULE-EVENT-002(player, 4, sector, slot, 0)
```

## Outputs

No return value. Records one type-4 report for `player`.

## Edge cases

None known.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Event), lists fully influencing a site among the events the Last Turn Events panel shows.

## Differences between builds

None known.

## Open questions

None known.
