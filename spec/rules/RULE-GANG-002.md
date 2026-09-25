---
id: RULE-GANG-002
title: A gang that dies or is terminated has only its sector byte set to inactive
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-GANG-003, FND-MOVE-001]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

When a gang dies in combat or is terminated, the game marks its roster slot as
empty and changes nothing else in the record. The gang's items stay in the
dead record, out of reach, until a new hire reuses the slot.

## When it runs

When combat damage leaves a gang with Force below 1, at the end of
`combat_phase`, and for each Terminate in `terminate_phase`
(RULE-TERMINATE-001).

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the gang record, of FMT-STATE-001.
- `died`: true when the gang died from damage, false for Terminate.

## Inputs

`casualties`.

## Procedure

```text
gang.sector = GANG_INACTIVE
if died:
    casualties[player] = casualties[player] + 1
```

## Outputs

No return value. Sets `sector` to 100 (`GANG_INACTIVE`). For a death, adds one
to `casualties[player]`. Leaves `weapon`, `armor`, `misc`, `force`, `action`
and every other field as they were.

## Edge cases

- The items left in the record are not returned to the player and cannot be
  given, sold or picked up.
- A hire into the slot copies a whole new record over it, so the old items
  disappear then.
- The Eliminate scenario's clean-up also retires gangs by writing only the
  sector byte, without counting a casualty (FND-GANG-003).

## What the sources say

SRC-MANUAL-GOG, page 30 (Equip), says a killed gang's equipment is gone forever,
and page 34 (Terminate) says Terminate removes the gang with all its items.
Both hold for play: the items are unreachable, though they remain in the
record.

## Differences between builds

None known.

## Open questions

- Where `casualties` is kept and its type are not recorded.
- The combat rule that decides the death calls this rule; the damage step
  belongs to the combat rules.
