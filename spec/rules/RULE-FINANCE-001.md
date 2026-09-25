---
id: RULE-FINANCE-001
title: The Financial panel projects next turn's cash flow for the whole city or one sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-FINANCE-001, FND-FINANCE-002, FND-EQUIP-008, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EQUIP-003, RULE-UPKEEP-001, RULE-SITE-001, FMT-STATE-001, FMT-STATE-002, FMT-DATA-002]
---

## Summary

The Financial panel lists what the player's cash is expected to do by the next
turn, as eight rows: Upkeep of the gangs and queued hires, the price of the
queued hires, queued equipment purchases less queued sales, 3 for each Bribe,
1 for each owned sector, the Cash of completed sites, an estimate of Chaos
income, and the sum of all seven. The Sector variant counts only what happens
in one sector.

## When it runs

When the player opens the Financial panel from the main console, choosing City
or Sector (SCR-FINANCE-001). The Sector variant is chosen by passing a sector
number instead of -1.

## Parameters

- `player`: the viewing player slot.
- `sector_number`: the sector shown by the Sector variant, or -1 for the City
  variant.

## Inputs

`gangs` with each gang's `definition`, `sector`, `action`, `target`, `force`,
`chaos` and item slots, `gang_definitions`, `item_definitions`,
`site_definitions`, `hire_offers`, `hire_orders`, and `sectors` with each
sector's `owner`, `income`, `factory` and `sites`.

## Procedure

```text
define finance_rows(player, sector_number) -> INT32[]:
    let upkeep = 0
    let gang_count = 0
    let recruits = 0
    let equipment = 0
    let officials = 0
    let tax = 0
    let protection = 0
    let chaos = 0
    for offer in 0..3:
        let order = hire_orders[player * 3 + offer]
        let wanted = order >= 0
        if sector_number != -1:
            wanted = order == sector_number
        if wanted:
            let d = gang_definitions[hire_offers[player * 3 + offer]]
            recruits = recruits - d.force        # the hiring price
            upkeep = upkeep - d.upkeep
            gang_count = gang_count + 1
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        let d = gang_definitions[gang.definition]
        if sector_number != -1 and gang.action == ACTION_MOVE and gang.target == sector_number:
            gang_count = gang_count + 1
            upkeep = upkeep - d.upkeep
        let counted = gang.sector != GANG_INACTIVE
        if sector_number != -1:
            counted = gang.sector == sector_number
        if not counted:
            continue
        gang_count = gang_count + 1
        upkeep = upkeep - d.upkeep
        if gang.action == ACTION_BRIBE:
            officials = officials - 3
        else if gang.action == ACTION_CHAOS:
            let estimate = (sectors[gang.sector].income + gang.chaos + gang.force) / 3
            if sector_number != -1 or estimate > 0:
                if sectors[gang.sector].owner != player:
                    estimate = estimate / 2
                chaos = chaos + estimate
        else if gang.action == ACTION_EQUIP:
            let where = gang.sector
            if sector_number != -1:
                where = sector_number
            equipment = equipment - item_price(player, where, gang.target)
        else if gang.action == ACTION_SELL:
            if (gang.target & 1) != 0:
                equipment = equipment + item_definitions[gang.weapon].cost / 2
            if (gang.target & 2) != 0:
                equipment = equipment + item_definitions[gang.armor].cost / 2
            if (gang.target & 4) != 0:
                equipment = equipment + item_definitions[gang.misc].cost / 2
        else if gang.action == ACTION_TERMINATE:
            upkeep = upkeep + d.upkeep
            gang_count = gang_count - 1
        else if gang.action == ACTION_MOVE and sector_number != -1:
            upkeep = upkeep + d.upkeep
            gang_count = gang_count - 1
    for s in 0..64:
        if sectors[s].owner != player:
            continue
        if sector_number != -1 and s != sector_number:
            continue
        tax = tax + 1
        for each site in sectors[s].sites:
            let kind = site_definitions[site.definition]
            if site.progress >= kind.resistance:
                protection = protection + kind.cash
    let total = upkeep + recruits + equipment + officials + tax + protection + chaos
    return [upkeep, gang_count, recruits, equipment, officials, tax, protection, chaos, total]
```

## Outputs

Returns the eight amounts and the gang count, as `INT32`, and changes no
state. The panel draws them top to bottom as Gang Upkeep (with the gang count
in parentheses on the same row), New Recruits, Equipment, City Officials,
Sector Tax, Site Protection, Chaos (Estimate) and Cash Adjustment
(SCR-FINANCE-001). Makes no random draw.

## Edge cases

- Sector Tax and Site Protection together are what RULE-UPKEEP-001 will
  collect, `cash_yield` summed over the owned sectors.
- The Equipment row is a projection; an Equip still fails at resolution when
  cash is short at the gang's turn (RULE-EQUIP-001). It uses the same Factory
  price (RULE-EQUIP-003).
- The Equipment row credits half the Cost of every item selected for Sale,
  while the resolver pays only for the last one (BUG-SELL-001), so a sale of
  several items is shown as worth more than it pays.
- New Recruits is the hiring price of the queued hires, and their Upkeep is on
  the Gang Upkeep row with the other gangs; both carry the hires' count.
- A terminating gang's Upkeep is taken off the Gang Upkeep row and it leaves
  the count.
- The Chaos estimate is a third of `income + chaos + force` for each Chaos
  gang, halved outside the player's own sectors; the City variant leaves out
  values of 0 and below, the Sector variant does not.
- The Sector variant counts the gangs standing in the sector, gives back the
  Upkeep of those moving out, and adds those moving in.
- Cash Adjustment includes the Chaos estimate.

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

- The row labels are in the template images; the pairing of the eight drawn
  values with the manual's rows rests on their order (FND-FINANCE-002).
