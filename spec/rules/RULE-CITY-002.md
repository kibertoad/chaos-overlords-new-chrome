---
id: RULE-CITY-002
title: Each sector's three sites are drawn uniformly and redrawn until they differ and their modifiers stay within six either way
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CITY-002, FND-RNG-005]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-002, FMT-STATE-004, FMT-DATA-001]
---

## Summary

Each sector gets three different sites, drawn at random from the 21 ordinary
site definitions (19 in Armageddon, which leaves out the two research sites).
A second or third site is redrawn when it repeats one already chosen, or when
adding it would push the sector's total of any statistic modifier past +6 or
below -6.

## When it runs

Once per new match, inside RULE-SETUP-004, after RULE-CITY-001.

## Parameters

None.

## Inputs

`scenario`, `site_definitions`, and the state of `rng` through `roll`.

## Procedure

```text
define propose_site() -> INT32:
    let id = roll(21) - 1
    while scenario == 9 and (id == 4 or id == 8):
        id = roll(21) - 1
    return id

define sites_balanced(ids: INT32[]) -> INT32:
    let totals: INT32[14] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
    for each id in ids:
        let d = site_definitions[id]
        let mods: INT32[14] = [d.combat, d.defense, d.stealth, d.detect, d.chaos, d.control, d.heal, d.influence, d.research, d.strength, d.blade, d.range, d.fighting, d.martial_arts]
        for k in 0..14:
            totals[k] = totals[k] + mods[k]
    for k in 0..14:
        if totals[k] < -6 or totals[k] > 6:
            return false
    return true

for s in 0..64:
    let ids: INT32[] = []
    append(ids, propose_site())
    while count(ids) < 3:
        let id = propose_site()
        let duplicate = false
        for each other in ids:
            if other == id:
                duplicate = true
        if duplicate:
            continue
        append(ids, id)
        if not sites_balanced(ids):
            remove_at(ids, count(ids) - 1)
    for k in 0..3:
        sectors[s].sites[k].definition = ids[k]
```

## Outputs

No return value. Sets the `definition` of all three site slots of every
sector. Makes one `roll(21)` for every proposal, including those rejected for
Armageddon, for a repeat or for balance.

## Edge cases

Slot 0 is never tested, so a single site may carry any modifiers. In
Armageddon, definitions 4 and 8 never appear, and each proposal that draws one
of them costs another draw before the duplicate and balance tests.

## What the sources say

SRC-MANUAL-GOG, page 41, describes sites and their modifiers and does not say
how they are placed.

## Differences between builds

None known.

## Open questions

- That the generator visits the sectors in ascending order, and places a
  sector's sites right after that sector's Income, is not recorded; the
  finding places all the density draws before any site draw.
- What each slot's `progress` is set to is not recorded.
- The names of the fourteen modifier fields come from FMT-DATA-001, whose
  offsets rest on an outside source.
