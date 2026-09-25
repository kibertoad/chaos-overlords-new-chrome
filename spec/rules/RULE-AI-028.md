---
id: RULE-AI-028
title: Family-10 computer gangs improve armor, equip item 44, heal, seek Stealth sites, then raise Chaos or hide
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-037, FND-AI-019, FND-AI-026, FND-AI-028]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

Family 10 is a Siege defender. A family-10 gang buys armor with a better
Defense when it can afford it, fills an empty miscellaneous slot with item 44
once that item is researched, and heals when it sees nobody. Otherwise it moves
toward sectors whose finished sites give more Stealth, and in the end raises
Chaos, or hides if another of its player's gangs in the sector already carries
on Chaos.

## When it runs

From RULE-AI-002, for a gang whose family is 10.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`, `misc`) and in
`planning_records` (`armor_cooldown`), `sector_weight`, `sectors` (`sites`),
`site_definitions` (`stealth`), `item_definitions` (`cost`),
`research_remaining`, `cash`, `rng_state` through `roll`, and what the
functions it calls read.

## Procedure

```text
# The sum of the positive Stealth of the finished sites in sector c (selector 8)
define stealth_sum(c):
    let n = 0
    for k in 0..3:
        let v = site_definitions[sectors[c].sites[k].definition].stealth
        if v > 0 and not site_unfinished(c, k):
            n = n + v
    return n

let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let ar = armor_defense_upgrade(player, idx)
if ar != -1 and r.armor_cooldown <= 0 and item_definitions[ar].cost <= cash[player]:
    plan(idx, ACTION_EQUIP, ar, 0)
    r.armor_cooldown = 2
else if g.misc == -1 and research_remaining[44 * 6 + player] == 0:
    plan(idx, ACTION_EQUIP, 44, 0)
else if g.force < 10 and g.heal >= -3 and sector_weight[player * 64 + s] == 0:
    plan(idx, ACTION_HEAL, 0, 0)
else:
    let probe = select_sector(player, 9, idx)
    if stealth_sum(probe) > stealth_sum(s):
        plan(idx, ACTION_MOVE, select_sector(player, 9, idx), 0)
    else if previous_action_count(player, s, ACTION_CHAOS) == 0:
        plan(idx, ACTION_CHAOS, 0, 0)
    else:
        plan(idx, ACTION_HIDE, 0, 0)
```

## Outputs

`stealth_sum` returns an integer. The handler returns nothing; it writes the
gang's planned action and targets through `plan` (Equip targets the item, Move
the sector the second `select_sector` call returns). An armor Equip sets the
armor cooldown to 2. Draws only inside `select_sector`, which it calls twice
when it considers a move.

## Edge cases

The item 44 Equip makes no Tech or cash test, so the order can fail at
resolution for lack of cash. With a tie for the best mode-9 score the two
selector calls each make a tie-break draw, and the second can pick a different
sector from the one the comparison used. Heal needs a weight of exactly 0, so a
gang that sees any other gang never heals.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- That a clear research value for item 44 means a remaining research of 0 is
  assumed.
- That the finished sites for `stealth_sum` are those with no Resistance left,
  the reverse of `site_unfinished`, is assumed.
- Whether family 10 has a Greed override or a family change is not recorded;
  FND-AI-037 lists none.
