---
id: RULE-EQUIP-003
title: An item's price is its Cost, less a third of it rounded down when the buyer owns the sector and its Factory is complete
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EQUIP-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-002, FMT-DATA-003]
---

## Summary

Equipment costs its listed Cost. In a sector the buyer owns and whose Factory
is complete, it costs a third less, with the third rounded down, so the price
is rounded toward the full Cost.

## When it runs

Whenever a price is needed: when an Equip is carried out (RULE-EQUIP-001), and
when the Equip list and the Financial panel show a price.

## Parameters

- `player`: the buying player slot.
- `sector_number`: the sector the buying gang is in, 0 to 63.
- `item`: the item's record number in `item_definitions`.

## Inputs

`item_definitions` and `sectors`.

## Procedure

```text
define item_price(player, sector_number, item) -> INT32:
    let cost = item_definitions[item].cost
    let sector = sectors[sector_number]
    if sector.factory != 0 and sector.owner == player:
        return cost - cost / 3
    return cost
```

## Outputs

Returns the price, an `INT32`. Changes no state and makes no random draw.

## Edge cases

- `/` truncates toward zero; Costs are not negative, so the discount is
  rounded down. A Cost of 11 gives 8, 12 gives 8, 2 gives 2 and 3 gives 2.
- The discount is not 30 percent and not `floor(Cost * 70 / 100)`, which would
  give 7 for a Cost of 11.
- Another player's completed Factory in the sector gives no discount, and
  neither does a Factory completed during this turn's `instant_phase`, since
  the sector's Factory flag is set only when the sector records are rebuilt
  before planning (FND-GANG-001, RULE-SITE-001).

## What the sources say

SRC-MANUAL-GOG, page 33 (Sell), says Factories give discounts on items, and the
manual's list of sites names the Factory as the site that lowers equipment
prices. It does not give the amount of the discount.

## Differences between builds

None known.

## Open questions

- That the sector is the buying gang's current one is taken from the manual;
  FND-EQUIP-001 does not name the sector index the resolver uses.
- The Equip list and the Financial panel are expected to use the same
  function; the reads at `0x0043F36D` and in the Financial panel have not been
  tied to them by a finding.
