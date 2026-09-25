---
id: RULE-INFLUENCE-001
title: Each Influence gang rolls on its own and adds its successes to the site's progress at once
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-001, FND-TURN-007, FND-STATE-002, FND-TURN-009, FND-AI-007, FND-GANG-001, FND-INFLUENCE-001, FND-INFLUENCE-002, FND-INFLUENCE-003, FND-CONTROL-001, FND-EVENT-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004, FMT-DATA-001]
---

## Summary

A gang influencing a site rolls dice equal to its Force plus its Influence and
adds each success to the site's progress. When progress reaches the site's
Resistance the site is complete and cooperates with whoever owns the sector.
Gangs influencing the same site act one after another, and once the site is
complete the rest roll nothing.

## When it runs

During `instant_phase`, for each gang whose `action` is `ACTION_INFLUENCE`, at
that gang's place in the phase's player and roster order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the acting gang, FMT-STATE-001.

## Inputs

The gang's `sector`, `target`, `force` and `influence` (the effective value
rebuilt at `turn_start`); the site slot's `definition` and `progress`; the
`resistance` of the site's entry in `site_definitions`;
`difficulty_band[player]`; and the state of `rng` through `roll`.

## Procedure

```text
let site = sectors[gang.sector].sites[gang.target]
let resistance = site_definitions[site.definition].resistance
if site.progress != resistance:
    let pool = gang.force + gang.influence
    let threshold = 5
    if difficulty_band[player] == 0:
        pool = pool - pool / 5
    else if difficulty_band[player] == 2:
        threshold = 4
    let successes = 0
    for d in 0..pool:
        if roll(6) >= threshold:
            successes = successes + 1
    site.progress = min(site.progress + successes, resistance)
    if site.progress == resistance:
        emit SiteCooperationAchieved(player, gang.sector, gang.target)
```

## Outputs

No return value. Adds the gang's successes to the site slot's `progress`,
never past the definition's Resistance, and emits `SiteCooperationAchieved`
when that makes the site complete. Makes one `roll(6)`, three draws from
`rng`, for each die of the pool, and none when the site was already complete.

## Edge cases

- A site completed by an earlier gang in the same phase is skipped by every
  later gang, which makes no draws.
- The effective `influence` was rebuilt before planning, so a site completed
  in this phase does not add its own bonuses to later gangs until the next
  `turn_start` (RULE-SITE-001).
- For a band-0 player the pool loses a fifth, rounded toward zero: a pool of 4
  keeps all four dice, a pool of 5 keeps four.
- A pool of 0 or less rolls nothing.
- The site record has no field naming who influenced it; a complete site
  benefits whoever owns the sector. When the owner changes, the Control pass
  sets every site's progress in the sector to 0 (FND-CONTROL-001), so the new
  owner has to influence each site again from nothing.

## What the sources say

SRC-MANUAL-GOG, page 31, describes the Influence command as winning over a site
in a sector the player controls, and pages 49 and 50 give the roll as the total
of Force plus Influence of all the player's gangs influencing the site, each
success taking one from the site's Resistance, the site being influenced at 0,
and all sites of a sector being lost with its control and back at full
Resistance when it is retaken. Page 48 counts a 5 or a 6 as a success. The
executable rolls each gang separately rather than as one pool, which gives the
same number of dice for bands 1 and 2 but applies the band-0 reduction to each
gang's pool. It counts progress up toward the Resistance rather than down, and
uses 4 or more for band 2.

## Differences between builds

None known.

## Open questions

- The report itself is RULE-EVENT-006, the handler of `SiteCooperationAchieved`.
- The picker's own rule for which sites can be chosen is in SCR-INFLUENCE-001.
  The resolver's case does not check that the player owns the sector
  (FND-TURN-007); what stops an Influence order in a sector the player does not
  own is the `turn_start` clearing of recurring orders and the command menus,
  which are not described here.
