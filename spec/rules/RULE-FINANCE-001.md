---
id: RULE-FINANCE-001
title: The Financial panel projects next turn's cash flow for the whole city or one sector
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EQUIP-003, RULE-UPKEEP-001, RULE-SITE-001, FMT-STATE-001, FMT-STATE-002, FMT-DATA-002]
---

## Summary

The Financial panel lists what the player's cash is expected to do by the next
turn: Upkeep for gangs and new recruits, queued equipment, Bribes, sector tax,
site cash, an estimate of Chaos income, and the sum of all of them. The Sector
variant does the same for one sector.

## When it runs

When the player opens the Financial panel from the main console, choosing City
or Sector (SCR-FINANCE-001).

## Parameters

- `player`: the viewing player slot.
- `sector_number`: the sector shown by the Sector variant, or -1 for the City
  variant.

## Inputs

`gangs`, `gang_definitions`, `sectors` with each sector's `owner` and
`cash_yield`, and each gang's `sector`, `action` and queued item.

## Procedure

```text
define finance_upkeep(player, sector_number) -> INT32:
    let total = 0
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector == GANG_INACTIVE:
            continue
        if sector_number == -1 or gang.sector == sector_number:
            total = total + gang_definitions[gang.definition].upkeep
    return total

define finance_equipment(player, sector_number) -> INT32:
    let total = 0
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector == GANG_INACTIVE or gang.action != ACTION_EQUIP:
            continue
        if sector_number == -1 or gang.sector == sector_number:
            total = total + item_price(player, gang.sector, gang.target)
    return total

define finance_sector_tax(player, sector_number) -> INT32:
    let total = 0
    for s in 0..64:
        if sectors[s].owner == player and (sector_number == -1 or s == sector_number):
            total = total + 1
    return total

define finance_site_protection(player, sector_number) -> INT32:
    let total = 0
    for s in 0..64:
        if sectors[s].owner == player and (sector_number == -1 or s == sector_number):
            total = total + sectors[s].cash_yield - 1
    return total
```

## Outputs

Each function returns an `INT32` and changes no state. The panel draws costs in
red and income in green. The New Recruits, City Officials, Chaos (Estimate) and
Cash Adjustment rows are not described closely enough to write down; see Open
questions.

## Edge cases

- Sector Tax and Site Protection together are what RULE-UPKEEP-001 will
  collect, `cash_yield` summed over the owned sectors.
- The Equipment row is a projection; an Equip still fails at resolution when
  cash is short at the gang's turn (RULE-EQUIP-001).

## What the sources say

SRC-MANUAL-GOG, pages 23 and 24 (Finances), says the panel breaks down the
projected cash flow for the next turn, with costs in red and income in green.
Its rows are Gang Upkeep; New Recruits, the Upkeep of gangs hired this turn and
their number; Equipment, the cost of equipping gangs; City Officials, the cost
of the Bribes the player's gangs attempt; Sector Tax, 1 cash for each
controlled sector; Site Protection, the cash from influenced sites in the
player's sectors; Chaos (Estimate), a rough estimate of the Chaos income; and
Cash Adjustment, the total of all of these. The Sector variant is the same for
one sector, which helps judge a sector's Chaos against its Tolerance.

## Differences between builds

None known.

## Open questions

- No finding describes how the executable computes any row; everything above
  is from the manual. The procedure's scope test for the Sector variant, and
  the use of `target` as the queued item, are assumptions.
- How the New Recruits, City Officials and Chaos (Estimate) rows are computed,
  and whether Cash Adjustment includes the Chaos estimate.
- Which screen row holds which amount (FND-FINANCE-001 gives eight row
  positions without names).
- Whether Equipment uses the Factory price; `0x0044D1BB` computes
  `cost - cost / 3` (FND-EQUIP-001), which suggests it does.
