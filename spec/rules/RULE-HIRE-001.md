---
id: RULE-HIRE-001
title: Hires and snubs are carried out player by player and offer slot by offer slot
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HIRE-001, FND-HIRE-002, FND-HIRE-005, FND-HIRE-006, FND-EQUIP-006, FND-EVENT-001, FND-TURN-005, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-001, FMT-DATA-002]
---

## Summary

At the end of the turn each player's hire or snub order is carried out. A
snubbed offer is removed. A hire fails if the player already has six gangs in
the sector, cannot pay, or has no free roster place; otherwise the new gang
appears in the sector with Force 5 to 9 and the player pays its hire cost. A gang whose hire cost is 0 is hired
whatever the player's cash.
Either way the offer's slot stays empty until the player's next planning phase.

## When it runs

Once per turn, as `hire_phase`, after `chaos_payout_phase`.

## Parameters

None.

## Inputs

`turn_order`, `hire_offers`, `hire_orders`, `gangs` (the `sector` of each of
the player's records), `cash`, `cash_spent`, `hire_force_modifier`, the
`hire_cost` and statistics of each entry of `gang_definitions`, and the state
of `rng` through `roll`.

## Procedure

```text
for each player in turn_order:
    for slot in 0..3:
        let i = player * 3 + slot
        let order = hire_orders[i]
        if order == -2:
            # snub
            hire_offers[i] = -hire_offers[i]
            hire_orders[i] = -1
        else if order >= 0:
            let here = 0
            for s in 0..81:
                if gangs[player * 81 + s].sector == order:
                    here = here + 1
            if here >= 6:
                emit HireSectorFull(player, order)
                hire_orders[i] = -1
                continue
            let chosen = hire_offers[i]
            let def = gang_definitions[chosen]
            let cost = def.hire_cost
            if cost != 0 and cost > cash[player]:
                emit HireCashShort(player, chosen)
                hire_orders[i] = -1
                continue
            let force = 10
            if hire_force_modifier[player] == 0:
                force = roll(5) + 4
            let free = -1
            for s in 0..80:
                if gangs[player * 81 + s].sector == GANG_INACTIVE:
                    free = s
                    break
            if free == -1:
                emit HireRosterFull(player, chosen)
                hire_orders[i] = -1
                continue
            let recruit = new FMT-STATE-001
            recruit.player = player
            recruit.definition = chosen
            recruit.sector = order
            for p in 0..6:
                recruit.visible_to[p] = 0
            recruit.visible_to[player] = 1
            recruit.force = force
            recruit.weapon = -1
            recruit.armor = -1
            recruit.misc = -1
            recruit.combat = INT8(def.combat)
            recruit.defense = INT8(def.defense)
            recruit.stealth = INT8(def.stealth)
            recruit.detect = INT8(def.detect)
            recruit.action = 0
            recruit.target = 0
            recruit.target_2 = 0
            recruit.repeat_action = 0
            recruit.repeat_target = 0
            recruit.chaos = INT8(def.chaos)
            recruit.control = INT8(def.control)
            recruit.heal = INT8(def.heal)
            recruit.influence = INT8(def.influence)
            recruit.research = INT8(def.research)
            recruit.strength = INT8(def.strength)
            recruit.blade = INT8(def.blade)
            recruit.ranged = INT8(def.range)
            recruit.fighting = INT8(def.fighting)
            recruit.martial_arts = INT8(def.martial_arts)
            gangs[player * 81 + free] = recruit
            cash_spent[player] = cash_spent[player] + cost
            cash[player] = cash[player] - cost
            hire_offers[i] = -hire_offers[i]
            hire_orders[i] = -1
```

## Outputs

No return value. For each snub, negates the offer and clears the order. For
each successful hire, writes a new gang record into the first free roster
slot, marks that gang for the network update (FND-HIRE-006), adds the hire
cost to `cash_spent` and subtracts it from `cash`,
negates the offer and clears the order. For each failed hire, clears the order
and emits `HireSectorFull`, `HireCashShort` or `HireRosterFull`, leaving the
offer in place. Makes three draws from `rng` (one `roll(5)`) for each hire that
passes the sector and cash tests, unless the player's `hire_force_modifier` is
set; a hire that then finds no free slot has still made them.

## Edge cases

- A hire with exactly as much cash as its cost succeeds.
- A definition whose hire cost is 0 skips the cash test, so it is hired even
  by a player in debt. The shipped `DATA/Gangs` has one such definition
  among the offered numbers 1 to 89 (FND-HIRE-006).
- The search stops at roster slot 79, so slot 80 is never filled by a hire
  and a player holds at most 80 hired gangs.
- The new gang starts with its definition's statistics, without items or
  site bonuses; its effective statistics are rebuilt at the next
  `turn_start` (RULE-GANG-001).
- The sector count covers only the hiring player's gangs; other players'
  gangs in the sector do not count (FND-HIRE-002).
- A failed hire keeps its offer, which stays positive and so is not refilled.
- Each count covers one player, so one player's hire never changes whether
  another player's hire fits.
- The new gang pays no Upkeep this turn; the next `upkeep_phase` charges it.
- A player may hold at most one hire or snub order per turn (RULE-HIRE-003),
  so at most one gang per player is hired per turn.

## What the sources say

SRC-MANUAL-GOG, pages 17 and 18, says the Hire panel offers three gangs, that
a hired gang's slot gets a new gang next turn, that the player may instead
fire an unwanted offer, that each Overlord commands at most 80 gangs and each
sector holds at most six of one player's gangs, and that a new gang starts
with Force from five to nine. Page 44 places hiring in a Hire phase after the
Execution phase. The manual agrees with the executable on all of these.

## Differences between builds

None known.

## Open questions

- The failure reports are RULE-EVENT-009, RULE-EVENT-010 and RULE-EVENT-011.
  FND-EVENT-001 lists no report for a snub or a successful hire.
- `hire_phase` coming after `control_phase` is not shown.
