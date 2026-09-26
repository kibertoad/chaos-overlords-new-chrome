---
id: RULE-COMBAT-001
title: A gang's Combat takes the skills that match its weapon when its statistics are rebuilt
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMBAT-008, FND-EXE-004, FND-GANG-007, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-GANG-001, RULE-ATTACK-001, FMT-STATE-001, FMT-DATA-003]
---

## Summary

A gang fights with its Combat, and its Combat includes the skills that go with
its weapon: Strength for item type 0, Strength and Blade for type 1, Ranged
for type 2, and Strength, Fighting and Martial Arts bare handed. The skills
are added once, when the gang's statistics are rebuilt at the top of each
turn, from the statistics just rebuilt.

## When it runs

Inside RULE-GANG-001, in `fn_0047781F` (range in FND-EXE-004), after the
other thirteen statistics and the definition, item and site parts of Combat
have been rebuilt [FND-GANG-007]. The attack and retaliation pools of
RULE-ATTACK-001 read the stored `combat` and compute no rating of their own
[FND-COMBAT-008].

## Parameters

- `g`, of FMT-STATE-001: the gang, with its thirteen other statistics already
  rebuilt.

## Inputs

The gang's rebuilt `strength`, `blade`, `ranged`, `fighting` and
`martial_arts`, its `weapon`, and the weapon's `type` in `item_definitions`.

## Procedure

```text
define combat_rating(g: FMT-STATE-001):
    if g.weapon == -1:
        return g.strength + g.fighting + g.martial_arts
    let kind = item_definitions[g.weapon].type
    if kind == ITEM_TYPE_MELEE:
        return g.strength
    if kind == ITEM_TYPE_BLADE:
        return g.strength + g.blade
    if kind == ITEM_TYPE_RANGED:
        return g.ranged
    return 0
```

## Outputs

Returns the skill term, an integer, which RULE-GANG-001 adds to `combat`.
Changes nothing and makes no draws.

## Edge cases

- The skills are the effective ones, with items and the owned sector's sites
  already added, so an item or site that raises Strength also raises Combat
  [FND-GANG-007].
- A weapon of any type other than 0, 1 and 2 adds no skill.
- Negative skills lower Combat.
- The term is part of the stored Combat, so everything else that reads
  `combat` (the computer players' estimates, the gang panels) sees it too.
- A hire fills the new gang's statistics from its definition alone, without
  this term, until the next rebuild [FND-GANG-007].

## What the sources say

SRC-MANUAL-GOG, numbered page 37 (Combat Skills), says Strength adds to combat
bare handed and with melee and blade weapons, Blade with blade weapons, Range
with ranged weapons, and Fighting and Martial Arts bare handed. Page 51 says a
gang's combat is its Combat statistic with all item modifiers plus the skill
that fits its weapon. This agrees with the executable when types 0, 1 and 2
are melee, blade and ranged.

## Differences between builds

None known.

## Open questions

- That item types 0, 1 and 2 are the manual's melee, blade and ranged classes
  follows from which skills each adds; no finding ties the values to the
  classes' names in the game's texts.
