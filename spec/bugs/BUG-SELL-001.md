---
id: BUG-SELL-001
title: Selling several items at once pays for only one of them
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-EQUIP-002, FND-EQUIP-007, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SELL-001, SCR-SELL-001]
---

## Symptom

A gang sells two or three items in one Sell order. All of them disappear, but
the player's cash grows by half the price of only one: the miscellaneous item
if it was sold, else the armor.

## Trigger conditions

A Sell order with more than one item selected on the Sell panel.

## Mechanism

The Sell branch of the transaction pass tests the weapon, armor and
miscellaneous bits in that order. Each selected branch empties its slot and
stores half that item's Cost in the same scratch local, replacing what the
previous branch stored. Cash and cash earned are then raised once by that local
(FND-EQUIP-002). The three stores are at `0x00474B3D`, `0x00474B81` and
`0x00474BC5` and the two additions at `0x00474BDF` and `0x00474BF3`
(FND-EQUIP-007); RULE-SELL-001 gives the procedure. The panel
lets the player select every filled slot, and the manual says each selected
item is sold for half its price.

## Frequency

Every multi-item Sell.

## Player reliance

None known. Selling one item per turn avoids it.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- Whether the designers meant a multi-item Sell to pay only one item. The
  assignment in place of an addition, and the manual's description, point to a
  mistake.
