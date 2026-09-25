---
id: RULE-COMBAT-001
title: A gang's combat rating adds the skills that match its weapon to its Combat
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

A gang fights with its Combat plus the skills that go with its weapon:
Strength for a melee weapon, Strength and Blade for a blade, Ranged for a
ranged weapon, and Strength, Fighting and Martial Arts bare handed.

## When it runs

Whenever RULE-ATTACK-001 computes an attack or retaliation pool.

## Parameters

- `g`, of FMT-STATE-001: the gang.

## Inputs

The gang's effective `combat`, `strength`, `blade`, `ranged`, `fighting`,
`martial_arts` and `weapon`, and the weapon's `type` in `item_definitions`.

## Procedure

```text
define combat_rating(g: FMT-STATE-001):
    if g.weapon == -1:
        return g.combat + g.strength + g.fighting + g.martial_arts
    let class = item_definitions[g.weapon].type
    # class 0 melee, 1 blade, 2 ranged: the numbering is not shown
    if class == 0:
        return g.combat + g.strength
    if class == 1:
        return g.combat + g.strength + g.blade
    return g.combat + g.ranged
```

## Outputs

Returns the combat rating, an integer. Changes nothing and makes no draws.

## Edge cases

Negative skills lower the rating.

## What the sources say

SRC-MANUAL-GOG, numbered page 37 (Combat Skills), says Strength adds to combat
bare handed and with melee and blade weapons, Blade with blade weapons, Range
with ranged weapons, and Fighting and Martial Arts bare handed. Page 51 says a
gang's combat is its Combat statistic with all item modifiers plus the skill
that fits its weapon. The executable is known to add a combat rating to Force
in the attack pool [FND-AI-007], but no finding yet shows how it builds that
rating.

## Differences between builds

None known.

## Open questions

- No finding shows the attack block's combat rating. The composition is from
  the manual only.
- The item class field of `item_definitions` and the values that mean melee, blade and
  ranged are not shown. FND-DATA-003 finds a type word at offset `0x7A` of each
  item record whose values group the items, but does not tie values to classes.
- Whether an unarmed gang's rating is read from its current or its base
  statistics.
