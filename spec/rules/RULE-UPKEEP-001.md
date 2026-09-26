---
id: RULE-UPKEEP-001
title: Upkeep charges each active gang its Upkeep and pays each owned sector's Cash byte, player by player
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UPKEEP-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SITE-001, FMT-STATE-001, FMT-STATE-002, FMT-DATA-002]
---

## Summary

At the start of every turn after the first, each player pays the Upkeep of each
of their gangs and collects the tax and site Cash of each sector they own. Cash
can go below zero.

## When it runs

`upkeep_phase`, in `turn_start`, before the sector records are rebuilt
(RULE-SITE-001). The first turn of a match skips it.

## Parameters

None.

## Inputs

`turn_order`, `gangs`, `gang_definitions`, `sectors` with the `cash_yield`
written by the previous rebuild, `cash`, `cash_earned` and `cash_spent`.

## Procedure

```text
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector == GANG_INACTIVE:
            continue
        let upkeep = gang_definitions[gang.definition].upkeep
        cash[player] = cash[player] - upkeep
        if upkeep < 0:
            cash_earned[player] = cash_earned[player] - upkeep
        else:
            cash_spent[player] = cash_spent[player] + upkeep
    for each sector in sectors:
        if sector.owner == player:
            cash[player] = cash[player] + sector.cash_yield
            if sector.cash_yield < 1:
                cash_spent[player] = cash_spent[player] - sector.cash_yield
            else:
                cash_earned[player] = cash_earned[player] + sector.cash_yield
```

## Outputs

No return value. Changes `cash`, `cash_earned` and `cash_spent` of every player
with a gang or a sector. Makes no random draw.

## Edge cases

- Each gang's and each sector's amount is classified on its own, so a positive
  and a negative amount never cancel before they reach `cash_earned` and
  `cash_spent`. A negative Upkeep counts as cash earned; a sector whose
  `cash_yield` is below 1 counts its amount, negated, as cash spent, which adds
  nothing for 0.
- Nothing stops `cash` going below 0.
- The sector's generated Income plays no part: what is collected is
  `cash_yield`, 1 plus the completed sites' Cash.
- A gang hired in the previous turn is active and pays.

## What the sources say

SRC-MANUAL-GOG, page 44 (The Structure of a Turn), says players pay Upkeep on
all their gangs and collect taxes from sectors and sites. Page 35 (Upkeep)
calls Upkeep the payment made to a gang each turn. Page 23 (Finances) gives 1
cash for each controlled sector as Sector Tax and the influenced sites' cash as
Site Protection, and notes that the Upkeep of a newly hired gang is paid for
the turn it is hired in. The executable's arithmetic agrees; how a hire's
first Upkeep is charged belongs to the hire rules.

## Differences between builds

None known.

## Open questions

- Whether eliminated players are skipped is not recorded; they have no gang and
  no sector, so the result is the same.
- Integer overflow of `cash` is not described by any finding.
