---
id: RULE-COMBAT-003
title: Damage Inflicted counts the full damage of every opening attack and no retaliation
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMBAT-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

A player's Damage Inflicted statistic grows by the whole damage each of its
gangs' opening attacks computes, even damage beyond what the target had left.
Retaliation and police damage are not counted.

## When it runs

Inside RULE-ATTACK-001, right after an opening attack's damage is added to the
target's damage for the phase [FND-COMBAT-003].

## Parameters

- `player`, the attacking gang's player slot.
- `damage`, the opening attack's damage.

## Inputs

`damage_inflicted`.

## Procedure

```text
damage_inflicted[player] = damage_inflicted[player] + damage
```

## Outputs

No return value. Raises `damage_inflicted[player]` by `damage`. No draws.

## Edge cases

Several attacks on one target in the same phase each count in full, even when
together they deal far more than the target's Force.

## What the sources say

SRC-MANUAL-GOG, numbered page 47, describes Damage Inflicted as the damage a
player's gangs dealt to opposing gangs in attacks, not counting retaliation.
That agrees with the executable. The manual does not say whether damage beyond
the target's Force counts.

## Differences between builds

None known.

## Open questions

- Whether an attack the target evaded adds anything (its record holds -1 as
  the damage).
