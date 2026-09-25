---
id: RULE-CHAOS-002
title: Chaos pays one cash per success, halved once per player and sector outside the player's own sectors
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CHAOS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002]
---

## Summary

After the transactions, each player is paid for Chaos: in each sector, the
successes of all the player's Chaos gangs there are added up, halved once
(rounding down) if the player does not own the sector, and added to the
player's cash.

## When it runs

As `chaos_payout_phase`, after `transaction_phase` and before
`terminate_phase` [FND-CHAOS-001].

## Parameters

None.

## Inputs

`chaos_successes` as RULE-CHAOS-001 left them, each gang's `sector`, each
sector's `owner`, `cash`.

## Procedure

```text
for each player in turn_order:
    let total: INT32[] = []
    for s in 0..64:
        append(total, 0)
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if chaos_successes[i] > 0 and gang.sector != GANG_INACTIVE:
            total[gang.sector] = total[gang.sector] + chaos_successes[i]
    for s in 0..64:
        let amount = total[s]
        if sectors[s].owner != player:
            amount = amount / 2
        cash[player] = cash[player] + amount
```

## Outputs

No return value. Raises each player's `cash` by its Chaos payout. No draws.

## Edge cases

- Halving happens once per player and sector, after the gangs' successes are
  added, so two gangs with 1 success each earn 1 cash outside the player's own
  sectors, where halving each gang separately would earn 0.
- A sector that cracked down this turn pays nothing, since RULE-CHAOS-001 set
  its gangs' successes to 0.
- Ownership is read at payout time, after Combat and before Control, so it is
  the ownership the sector had after any neutralization by this turn's
  Crackdowns.

## What the sources say

SRC-MANUAL-GOG, numbered pages 29, 30 and 49, says each success earns one cash,
and only half outside a sector the player controls. It does not say how the
half is rounded or whether it is taken per gang.

## Differences between builds

None known.

## Open questions

- Whether a Chaos gang that died in this turn's combat is still paid: its
  sector byte is 100 by then. The procedure skips it; the finding does not say
  how the payout finds a gang's sector.
- Whether the payout also raises `cash_earned`, and in what order it visits
  players and sectors.
