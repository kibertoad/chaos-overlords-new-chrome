---
id: RULE-TOLERANCE-001
title: A sector's Tolerance moves one point per turn back toward its normal value
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [SRC-MANUAL-GOG, SRC-RECHAOS-3561D41, FND-GANG-001, FND-CITY-001]
conflicting: []
split_with: []
related: [FMT-STATE-002, FMT-STATE-004, FMT-DATA-001]
---

## Summary

A sector's normal Tolerance comes from its income class, adjusted by the
Tolerance of the sites influenced there. After a Bribe or a Snitch has moved
it, Tolerance creeps back toward normal by one point each turn.

## When it runs

Once per turn. Where in the turn is not recorded.

## Parameters

None.

## Inputs

Each sector's `tolerance`, its base Tolerance (`unk_02`, see Open questions),
its three `sites`, and the `resistance` and `tolerance` of each site's entry
in `site_definitions`.

## Procedure

```text
for each sector in sectors:
    let normal = sector.unk_02
    for each s in sector.sites:
        let def = site_definitions[s.definition]
        if s.progress == def.resistance:
            normal = normal + def.tolerance
    if sector.tolerance > normal:
        sector.tolerance = sector.tolerance - 1
    else if sector.tolerance < normal:
        sector.tolerance = sector.tolerance + 1
```

## Outputs

No return value. Moves each sector's `tolerance` one point toward its normal
value, or leaves it when it is already there. Makes no random draw.

## Edge cases

- A sector that has lost control loses its completed sites (their progress
  returns to 0), so its normal value drops back to the base.
- The move is one point whatever the distance, so several Bribes take as many
  turns to wear off.

## What the sources say

SRC-MANUAL-GOG, page 48, gives each sector class its Income and base
Tolerance, the two adding up to 17. Page 29 and page 50 say Tolerance changed
by Bribe or Snitch returns to the sector's normal Tolerance, as modified by its
sites, by one point each turn. Page 41 says sites can raise or lower a
sector's Tolerance. SRC-RECHAOS-3561D41 names the sector record's byte at
`0x02` the base Tolerance. No finding yet shows the executable's step.

## Differences between builds

None known.

## Open questions

- Where the executable moves Tolerance toward normal, and when in the turn,
  has not been found. The sector recomputation of RULE-SITE-001 sums the completed
  sites' Tolerance (FND-GANG-001), which may be where the normal value is
  kept.
- Whether `unk_02` holds the base Tolerance, and whether it is the 17 minus
  Income that city generation computes (FND-CITY-001), is from
  SRC-RECHAOS-3561D41 only.
- Whether only a complete site's Tolerance counts, as written here, or the
  test also involves the sector's owner.
