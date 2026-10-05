---
id: RULE-AI-028
title: Family-10 computer gangs improve armor, equip item 44, heal, seek Stealth sites, then raise Chaos or hide
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-073, EXP-TURN-013, FND-AI-071, FND-AI-046, FND-AI-019, FND-AI-026, FND-AI-028, FND-EXE-004, FND-OBJECTIVE-003, FND-AI-055, EXP-TURN-057, EXP-TURN-075]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

Family 10 is a defender of Eliminate (scenario 7). A family-10 gang buys armor
with a better Stealth when it can afford it, fills an empty miscellaneous slot
with item 44 once that item is researched, and heals when it sees nobody.
Otherwise it moves when the sector that selector mode 9 picks has a last
finished site with more Stealth than the last finished site where it stands, and
in the end raises Chaos, or hides if another of its player's gangs in the
sector already carries on Chaos.

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
# The Stealth of the last finished site in sector c, 0 when none is (selector 8)
define last_finished_stealth(c):
    let n = 0
    for k in 0..3:
        if not site_unfinished(c, k):
            n = site_definitions[sectors[c].sites[k].definition].stealth
    return n

let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let ar = armor_stealth_upgrade(player, idx)
if ar != -1 and r.armor_cooldown <= 0 and item_definitions[ar].cost <= cash[player]:
    plan(idx, ACTION_EQUIP, ar, 0)
    r.armor_cooldown = 2
else if g.misc == -1 and research_remaining[44 * 6 + player] == 0:
    plan(idx, ACTION_EQUIP, 44, 0)
else if g.force < 10 and g.heal >= -3 and sector_weight[player * 64 + s] == 0:
    plan(idx, ACTION_HEAL, 0, 0)
else:
    let probe = select_sector(player, 9, idx)
    if last_finished_stealth(probe) > last_finished_stealth(s):
        plan(idx, ACTION_MOVE, select_sector(player, 9, idx), 0)
    else if previous_action_count(player, s, ACTION_CHAOS) == 0:
        plan(idx, ACTION_CHAOS, 0, 0)
    else:
        plan(idx, ACTION_HIDE, 0, 0)
```

## Outputs

`last_finished_stealth` returns an integer. The handler returns nothing; it writes the
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

`last_finished_stealth` is not a sum: a sector scores the Stealth of its last
finished site even when an earlier finished site has more, a negative Stealth
counts as it is, and a sector with no finished site scores 0. Mode 9 of the
sector selector scores sectors differently, by the sum of the positive Stealth
of their finished sites (RULE-AI-006), so the sector it returns can score no
better here than the gang's own [FND-AI-071].

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- That a clear research value for item 44 means a remaining research of 0 is
  assumed.
- Whether family 10 has a Greed override or a family change is not recorded;
  FND-AI-071 lists none.
