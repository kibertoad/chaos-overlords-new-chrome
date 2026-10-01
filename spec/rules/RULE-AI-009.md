---
id: RULE-AI-009
title: A computer player that hires nothing snubs one offer, the first in Greed and the least efficient elsewhere
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-065, FND-EXE-004]
conflicting: []
split_with: []
related: []
---

## Summary

When a computer player's ranking finds no offer it wants and can pay for, it
turns one offer away so that a new gang is offered next turn. In Greed it
always turns away the first offer; otherwise it turns away the gang whose
useful statistics, weighted by its Tech Level, are worst for their cost.

## When it runs

When RULE-AI-010 calls `offer_to_snub` after `rank_offer` returned -1.

## Parameters

None.

## Inputs

`hire_offers`, `gang_definitions` (`hire_cost`, `upkeep`, `tech_level`,
`combat`, `defense`, `chaos`, `control`, `heal`, `influence`, `research`,
`strength`, `blade`, `range`, `fighting`, `martial_arts`) and `scenario`.

## Procedure

```text
# The offer slot to snub
define offer_to_snub(player):
    if scenario == 0:
        return 0
    let best = 5000
    let choice = 0
    for k in 0..3:
        let d = gang_definitions[hire_offers[player * 3 + k]]
        let sum = max(d.combat, 0) + max(d.defense, 0) + max(d.chaos, 0) + max(d.control, 0)
        sum = sum + max(d.heal, 0) + max(d.influence, 0) + max(d.research, 0) + max(d.strength, 0)
        sum = sum + max(d.blade, 0) + max(d.range, 0) + max(d.fighting, 0) + max(d.martial_arts, 0)
        let value = d.tech_level * sum * 20 / (d.hire_cost + d.upkeep + 1)
        if value < best:
            best = value
            choice = k
    return choice
```

## Outputs

Returns the offer slot to snub, 0 to 2. The caller stores -2 (the snub order)
in that slot's element of `hire_orders`. Makes no draw.

## Edge cases

Equal values keep the earlier offer. When every value is 5000 or more, slot 0
is snubbed. A gang with Tech Level 0 or less has a value of 0 or less and is
snubbed before any gang with a positive Tech Level. The product is divided
once, truncating toward zero. In Greed the original's loop over the offers
leaves before its first pass, so the first offer is snubbed whatever the
offers are.

## What the sources say

SRC-MANUAL-GOG does not describe it.

## Differences between builds

None known.

## Open questions

- The address the snub value `0xFE` is written to is not recorded; it is taken
  to be the offer's element of `hire_orders`.
- Whether `hire_cost + upkeep + 1` can be 0 (a divide by zero) depends on the
  shipped gang values, which are content; the original's behaviour then is not
  recorded.
