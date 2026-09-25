---
id: RULE-HIRE-001
title: Hires and snubs are carried out player by player and offer slot by offer slot
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HIRE-001, FND-HIRE-002, FND-HIRE-005, FND-EQUIP-006, FND-EVENT-001, FND-TURN-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-001, FMT-DATA-002]
---

## Summary

At the end of the turn each player's hire or snub order is carried out. A
snubbed offer is removed. A hire fails if the player already has six gangs in
the sector, cannot pay, or has no free roster place; otherwise the new gang
appears in the sector with Force 5 to 9 and the player pays its hire cost.
Either way the offer's slot stays empty until the player's next planning phase.

## When it runs

Once per turn, as `hire_phase`, after `chaos_payout_phase`.

## Parameters

None.

## Inputs

`turn_order`, `hire_offers`, `hire_orders`, `gangs` (the `sector` of each of
the player's records), `cash`, `cash_spent`, `hire_force_modifier`, the
`force` of each entry of `gang_definitions`, which is the hire price,, and the state of `rng`
through `roll`.

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
            let cost = gang_definitions[chosen].force
            if cost > cash[player]:
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
            recruit.force = force
            # The other fields the original writes into the new record are not recorded.
            gangs[player * 81 + free] = recruit
            cash_spent[player] = cash_spent[player] + cost
            cash[player] = cash[player] - cost
            hire_offers[i] = -hire_offers[i]
            hire_orders[i] = -1
```

## Outputs

No return value. For each snub, negates the offer and clears the order. For
each successful hire, writes a new gang record into the first free roster
slot, adds the hire cost to `cash_spent` and subtracts it from `cash`,
negates the offer and clears the order. For each failed hire, clears the order
and emits `HireSectorFull`, `HireCashShort` or `HireRosterFull`, leaving the
offer in place. Makes three draws from `rng` (one `roll(5)`) for each hire that
passes the sector and cash tests, unless the player's `hire_force_modifier` is
set; a hire that then finds no free slot has still made them.

## Edge cases

- A hire with exactly as much cash as its cost succeeds.
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

- The hire price is taken to be `force` at `0x7A` of FMT-DATA-002, the field
  the computer players compare with cash (FND-AI-008). FND-HIRE-001 does not
  give the displacement the hire block reads.
- Which fields of the new gang record the original sets besides `player`,
  `definition`, `sector` and `force` (equipment, action and recurring
  action, `visible_to`, the effective statistics) is not recorded.
- Whether the free-slot search covers roster slots 0 to 79 or 1 to 80 is not
  written down; this rule assumes 0 to 79.
- The operand order of the cash comparison at `0x00475A0E` is inferred from
  the `JG` failure branch: `cost > cash` fails. What happens to a zero-cost
  hire when cash is negative follows from that reading and is unconfirmed.
- The failure reports are RULE-EVENT-009, RULE-EVENT-010 and RULE-EVENT-011.
  FND-EVENT-001 lists no report for a snub or a successful hire.
- Where in the successful path the offer is negated relative to the cash
  updates is not recorded; nothing between them reads it.
- `hire_phase` coming after `control_phase` is not shown.
