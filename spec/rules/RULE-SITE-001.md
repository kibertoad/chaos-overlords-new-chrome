---
id: RULE-SITE-001
title: Before planning, each sector record is rebuilt from its completed sites, whose bonuses go to the owner's gangs there
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-GANG-001, FND-STATE-001, FND-UPKEEP-001, FND-UI-035, FND-TURN-001, FND-CONTROL-001, SRC-MANUAL-GOG, SRC-RECHAOS-3561D41]
conflicting: []
split_with: []
related: [FMT-STATE-002, FMT-STATE-004, FMT-DATA-001]
---

## Summary

A site that has been fully influenced adds its Cash, Support, Tolerance and
statistic modifiers to its sector, and a site with a special effect sets the
sector's research level or Factory flag. The sector's owner collects the Cash
with the sector tax, and the owner's gangs in the sector get the statistic
modifiers. The totals are worked out again before every planning phase, from
the sector's base Income and base Tolerance and its sites, so a site completed
during a turn starts to count only at the next one.

## When it runs

In `turn_start`, for every sector, before each gang's effective statistics
are rebuilt, and in the first turn before the first `planning_phase`.

## Parameters

None.

## Inputs

Each sector's `base_income`, `base_tolerance` and three `sites`
(`definition`, `progress`), and for each site's entry in `site_definitions`
its `resistance`, `cash`, `support`, `tolerance`, `special` and the
fourteen statistic modifiers `combat`, `defense`, `stealth`, `detect`,
`chaos`, `control`, `heal`, `influence`, `research`, `strength`, `blade`,
`range`, `fighting` and `martial_arts`.

## Procedure

```text
for each sector in sectors:
    sector.cash_yield = 1
    sector.income = sector.base_income
    sector.tolerance = sector.base_tolerance
    sector.support = 0
    sector.research_level = 0
    sector.factory = 0
    sector.site_combat = 0
    sector.site_defense = 0
    sector.site_stealth = 0
    sector.site_detect = 0
    sector.site_chaos = 0
    sector.site_control = 0
    sector.site_heal = 0
    sector.site_influence = 0
    sector.site_research = 0
    sector.site_strength = 0
    sector.site_blade = 0
    sector.site_ranged = 0
    sector.site_fighting = 0
    sector.site_martial_arts = 0
    for each s in sector.sites:
        let def = site_definitions[s.definition]
        if s.progress >= def.resistance:
            sector.cash_yield = sector.cash_yield + def.cash
            sector.support = sector.support + def.support
            sector.tolerance = sector.tolerance + def.tolerance
            sector.site_combat = sector.site_combat + def.combat
            sector.site_defense = sector.site_defense + def.defense
            sector.site_stealth = sector.site_stealth + def.stealth
            sector.site_detect = sector.site_detect + def.detect
            sector.site_chaos = sector.site_chaos + def.chaos
            sector.site_control = sector.site_control + def.control
            sector.site_heal = sector.site_heal + def.heal
            sector.site_influence = sector.site_influence + def.influence
            sector.site_research = sector.site_research + def.research
            sector.site_strength = sector.site_strength + def.strength
            sector.site_blade = sector.site_blade + def.blade
            sector.site_ranged = sector.site_ranged + def.range
            sector.site_fighting = sector.site_fighting + def.fighting
            sector.site_martial_arts = sector.site_martial_arts + def.martial_arts
            if def.special == 1 and sector.research_level < 1:
                sector.research_level = 1
            else if def.special == 2 and sector.research_level < 2:
                sector.research_level = 2
            else if def.special == 3:
                sector.factory = 1
```

Each addition takes the site field as a signed 16-bit value and stores the
sum as a signed byte [FND-STATE-001].

## Outputs

No return value. Rewrites each sector's `cash_yield`, `income`, `tolerance`,
`support`, `research_level`, `factory` and fourteen `site_*` sums. Makes no random draw. The recomputation of each gang's effective
statistics that follows adds the fourteen sums to every gang in the sector
whose player owns the sector, and to no other gang (RULE-GANG-001).

## Edge cases

- A site counts when its progress is at least its definition's Resistance.
  The record holds no influencer, so a completed site benefits whoever owns
  the sector; when the owner changes, every site's progress is set to 0
  (FND-CONTROL-001) and the sector's sums fall back to nothing.
- Tolerance changes made by Bribe, Snitch and the return toward normal act on
  `base_tolerance` during resolution and reach `tolerance` only at the next
  run of this rule (FND-STATE-001).
- A site whose definition has Resistance 0 is complete at progress 0, so it
  counts for any owner without an Influence order, even right after the
  owner changes.
- A site completed during `instant_phase` changes nothing until the next
  `turn_start`: the Chaos, Combat, Transaction and Control passes of that
  turn still use the old sums.
- `cash_yield` is 1 plus the completed sites' Cash; it is the owner's income
  from the sector, not the Income from city generation (FND-UPKEEP-001).

## What the sources say

SRC-MANUAL-GOG, pages 41 and 42, says a fully influenced site affects all the
influencing player's gangs in its sector only, gives Support and Cash, can
change the sector's Tolerance, and that losing control of a sector loses its
sites. The executable ties the benefit to the sector's owner rather than to a
site owner. SRC-RECHAOS-3561D41 gives the offsets of the fourteen sums; the
executable agrees (FND-STATE-001).

## Differences between builds

None known.

## Open questions

- The site definition fields read here are placed in FMT-DATA-001 from its
  own evidence; this rule does not confirm their offsets.
- Where `turn_start` calls this recomputation relative to `upkeep_phase` is given
  by the TURN rules.
