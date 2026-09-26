---
id: RULE-GANG-001
title: Each active gang's fourteen statistics are its definition's, plus its items', plus its owned sector's completed sites', and Combat also takes the skills that go with its weapon
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-GANG-001, FND-GANG-007, FND-UPKEEP-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SITE-001, RULE-COMBAT-001, FMT-STATE-001, FMT-STATE-002, FMT-DATA-002, FMT-DATA-003]
---

## Summary

A gang's Combat, Defense and its other twelve statistics are worked out again
before every planning phase: the gang's own values, plus what each of its three
items adds, plus what the completed sites of its sector add when its player
owns that sector. Combat also takes the skills that go with the gang's weapon:
Strength, Fighting and Martial Arts bare handed, Strength with a melee weapon,
Strength and Blade with a blade, Ranged with a ranged weapon.

## When it runs

In `turn_start`, after the sector records have been rebuilt (RULE-SITE-001),
before `planning_phase`. It runs before the first turn's planning as well.

## Parameters

None.

## Inputs

`turn_order`, `gangs`, `gang_definitions`, `item_definitions`, `sectors`, and
the site sums RULE-SITE-001 has just written.

## Procedure

```text
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector == GANG_INACTIVE:
            continue
        let base = gang_definitions[gang.definition]
        gang.combat = base.combat
        gang.defense = base.defense
        gang.stealth = base.stealth
        gang.detect = base.detect
        gang.chaos = base.chaos
        gang.control = base.control
        gang.heal = base.heal
        gang.influence = base.influence
        gang.research = base.research
        gang.strength = base.strength
        gang.blade = base.blade
        gang.ranged = base.range
        gang.fighting = base.fighting
        gang.martial_arts = base.martial_arts
        for each item in [gang.weapon, gang.armor, gang.misc]:
            if item != -1:
                let bonus = item_definitions[item]
                gang.combat = gang.combat + bonus.combat
                gang.defense = gang.defense + bonus.defense
                gang.stealth = gang.stealth + bonus.stealth
                gang.detect = gang.detect + bonus.detect
                gang.chaos = gang.chaos + bonus.chaos
                gang.control = gang.control + bonus.control
                gang.heal = gang.heal + bonus.heal
                gang.influence = gang.influence + bonus.influence
                gang.research = gang.research + bonus.research
                gang.strength = gang.strength + bonus.strength
                gang.blade = gang.blade + bonus.blade
                gang.ranged = gang.ranged + bonus.range
                gang.fighting = gang.fighting + bonus.fighting
                gang.martial_arts = gang.martial_arts + bonus.martial_arts
        let sector = sectors[gang.sector]
        if sector.owner == player:
            gang.combat = gang.combat + sector.site_combat
            gang.defense = gang.defense + sector.site_defense
            gang.stealth = gang.stealth + sector.site_stealth
            gang.detect = gang.detect + sector.site_detect
            gang.chaos = gang.chaos + sector.site_chaos
            gang.control = gang.control + sector.site_control
            gang.heal = gang.heal + sector.site_heal
            gang.influence = gang.influence + sector.site_influence
            gang.research = gang.research + sector.site_research
            gang.strength = gang.strength + sector.site_strength
            gang.blade = gang.blade + sector.site_blade
            gang.ranged = gang.ranged + sector.site_ranged
            gang.fighting = gang.fighting + sector.site_fighting
            gang.martial_arts = gang.martial_arts + sector.site_martial_arts
        # the combat skills, read after items and sites have been added
        if gang.weapon == -1:
            gang.combat = gang.combat + gang.strength + gang.fighting + gang.martial_arts
        else:
            let kind = item_definitions[gang.weapon].type
            if kind == ITEM_TYPE_MELEE:
                gang.combat = gang.combat + gang.strength
            else if kind == ITEM_TYPE_BLADE:
                gang.combat = gang.combat + gang.strength + gang.blade
            else if kind == ITEM_TYPE_RANGED:
                gang.combat = gang.combat + gang.ranged
```

## Outputs

No return value. Rewrites the fourteen statistic fields of every active gang,
from `combat` to `martial_arts`. `force` is not changed. Makes no random draw.

## Edge cases

- Each field is an `INT8`, so a total outside -128 to 127 keeps only its low
  eight bits. No shipped combination is known to reach that.
- A site completed during this turn's `instant_phase` is not in the sector sums
  until the next rebuild, so it changes no dice pool in the turn it completes.
- A gang in a sector its player does not own gets nothing from the sites there,
  even from sites its player influenced before losing the sector.
- The resolver's dice pools read these fields: Heal uses `heal + 4` and
  Research `force + research` (FND-GANG-001).
- The combat skills added to `combat` are the rebuilt values, so items and
  sites that raise Strength, Blade, Ranged, Fighting or Martial Arts raise
  Combat too. The stored `combat` is the whole combat rating: the attack adds
  it to Force without adding the skills again (FND-GANG-007).
- A weapon whose type is not 0, 1 or 2 adds its own `combat` modifier and no
  skill; the Equip pass never puts such an item in the weapon slot
  (FND-GANG-007, RULE-EQUIP-001).
- A gang hired during a turn's resolution starts with its definition's values
  in these fields and is rebuilt with every other active gang before its
  first planning phase (FND-GANG-007).
- The pairing of the fourteen fields across the gang, item, sector and
  definition records is shown by the rebuild itself: each field is built from
  the fields at the same position in the other records (FND-GANG-007).

## What the sources say

SRC-MANUAL-GOG, pages 39 and 40 (Statistics/Skill Mods), says item modifiers
add to a gang's statistics and are cumulative, and page 42 (Stats/Site Mods)
says an influenced site adds its modifiers to every gang in the controlled
sector. Both agree with the executable. Pages 37 (Combat Skills) and 51 name
the skills that go with each kind of weapon; the executable adds them into the
stored Combat here.

## Differences between builds

None known.

## Open questions

None known.
