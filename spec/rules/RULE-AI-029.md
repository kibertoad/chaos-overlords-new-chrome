---
id: RULE-AI-029
title: Family-11 computer gangs equip, heal, attack the first visible definition-0 gang, or move in blocks of six behind a leader
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-024, FND-AI-021, FND-AI-015, FND-AI-027, FND-AI-028, FND-EXE-004, FND-OBJECTIVE-003, FND-AI-055, FND-AI-061]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, RULE-AI-006, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Family 11 is the Eliminate formation (scenario 7). A family-11 gang buys a
better weapon, armor or Detect item and heals when hurt. In land its player
owns it moves on through sector selector mode 10. In other land it attacks the
first gang there with gang definition 0 that it can see; otherwise the
family-11 gangs move in blocks of six, where the first of each block picks the
destination through mode 10 and the others follow it through mode 16.

## When it runs

From RULE-AI-002, for a gang whose family is 11.

## Parameters

`player`: the computer player. `slot`: the gang's roster slot.

## Inputs

The gang's record in `gangs` (`sector`, `force`, `heal`) and in
`planning_records` (`previous_action`, `weapon_cooldown`, `armor_cooldown`),
the other players' `gangs` (`sector`, `definition`, `visible_to`), `sectors`
(`owner`), `item_definitions` (`cost`), `cash`, and what the functions it
calls read.

## Procedure

```text
# The first gang of another player in sector s that the player can see and
# whose definition is 0, or -1 (selector 0xAC)
define first_visible_definition_zero(player, s):
    for p in 0..6:
        if p == player:
            continue
        for k in 0..81:
            let d = gangs[p * 81 + k]
            if d.sector == s and d.visible_to[player] != 0 and d.definition == 0:
                return p * 81 + k
    return -1

let idx = player * 81 + slot
let g = gangs[idx]
let r = planning_records[idx]
let s = g.sector
let not_after_attack = r.previous_action != ACTION_ATTACK
let wp = weapon_upgrade(player, idx)
let ar = armor_upgrade(player, idx)
let mi = misc_detect_upgrade(player, idx)
if wp != -1 and r.weapon_cooldown <= 0 and not_after_attack:
    plan(idx, ACTION_EQUIP, wp, 0)
    r.weapon_cooldown = item_definitions[wp].cost * 3
    aux_records[idx].focus = s
else if ar != -1 and r.armor_cooldown <= 0 and not_after_attack:
    plan(idx, ACTION_EQUIP, ar, 0)
    r.armor_cooldown = item_definitions[ar].cost * 3
    aux_records[idx].focus = s
else if mi != -1 and item_definitions[mi].cost <= cash[player] and not_after_attack:
    plan(idx, ACTION_EQUIP, mi, 0)
    aux_records[idx].focus = s
else if g.force < 8 and g.heal >= -3 and not_after_attack:
    plan(idx, ACTION_HEAL, 0, 0)
    aux_records[idx].focus = s
else if owner_query(s) == player:
    plan(idx, ACTION_MOVE, select_sector(player, 10, idx), 0)
    aux_records[idx].focus = s
else:
    let t = first_visible_definition_zero(player, s)
    if t >= 0:
        plan(idx, ACTION_ATTACK, t / 81, t % 81)
        aux_records[idx].focus = s
    else if is_block_leader(player, slot):
        let dest = select_sector(player, 10, idx)
        plan(idx, ACTION_MOVE, dest, 0)
        aux_records[idx].focus = dest
    else:
        plan(idx, ACTION_MOVE, select_sector(player, 16, idx), 0)
        aux_records[idx].focus = s
```

## Outputs

`first_visible_definition_zero` returns an index into `gangs` or -1. The
handler returns nothing; it writes the gang's planned action and targets
through `plan` (Equip targets the item, Attack the found gang's player and
roster slot, Move the sector `select_sector` returns), and sets `focus` to the
destination for a block leader's Move and to the current sector for every
other action. A weapon or armor Equip sets the matching cooldown to three times
the item's cost. Makes no draw of its own; draws only inside `select_sector`.

## Edge cases

The attack is made without a strength test, on the first qualifying gang in
player slot and roster slot order. Block leadership counts inactive records
whose family byte is still 11 (RULE-AI-006), so blocks can be shorter than six
active gangs. A block leader that moves stores its one-step destination, and
its followers score that sector +1 in mode 16. A gang whose previous action
was Attack neither equips nor heals, so it attacks again or moves. The
miscellaneous Equip sets no cooldown. Under police presence the owner query is
-2, so a gang in its own sector goes on to the attack and the block Moves.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' strategies.

## Differences between builds

None known.

## Open questions

- The byte selector `0xAC` requires to be 0 is the gang's `definition` in
  FMT-STATE-001, which reads as a test for the Right Hands; older notes call it
  a state byte.
