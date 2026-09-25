---
id: RULE-EVENT-003
title: An elimination is reported to all six player slots
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-003, FND-EVENT-001]
conflicting: []
split_with: []
related: [RULE-EVENT-002]
---

## Summary

When a player is knocked out of the match, every player slot gets a report of
it, whether or not a player sits in that slot.

## When it runs

In `turn_end`, after the elimination check has cleared `player_active` for
each eliminated player and before the end of the match is evaluated.

## Parameters

- `was_active`, of type `UINT8[6]`: each player's `player_active` as it was before the
  elimination check.

## Inputs

`player_active`, `turn_order`.

## Procedure

```text
for each player in turn_order:
    if was_active[player] and not player_active[player]:
        for recipient in 0..6:
            call RULE-EVENT-002(recipient, 9)
```

## Outputs

No return value. Records one type-9 report in each of the six slots for each
player eliminated this turn: all six reports for the lowest eliminated slot
first, then the next.

## Edge cases

- Empty slots, computer players and the eliminated player also receive the
  report.
- Two players eliminated in the same turn give each slot two reports.

## What the sources say

SRC-MANUAL-GOG does not describe the elimination report.

## Differences between builds

None known.

## Open questions

- The arguments the report carries, such as which player was eliminated, are
  not recorded.
