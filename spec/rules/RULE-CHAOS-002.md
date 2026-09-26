---
id: RULE-CHAOS-002
title: Chaos pays one cash per success, halved once per player and sector outside the player's own sectors
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-CHAOS-001, FND-CHAOS-002, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002]
---

## Summary

After the transactions, each player is paid for Chaos: in each sector, the
successes of all the player's Chaos gangs there are added up, halved once
(rounding down) if the player does not own the sector, and added to the
player's cash and to the cash it has earned.

## When it runs

As `chaos_payout_phase`, after `transaction_phase` and before
`terminate_phase`, in the resolver `fn_00472775` at `0x00474E57..0x00475091`
(range in FND-EXE-004) [FND-CHAOS-001, FND-CHAOS-002].

## Parameters

None.

## Inputs

`chaos_successes` as RULE-CHAOS-001 left them, each gang's `sector` and
`action`, each sector's `owner`, `cash`, `cash_earned`.

## Procedure

```text
let total: INT32[] = []
for n in 0..384:
    append(total, 0)
for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_CHAOS:
            total[player * 64 + gang.sector] = total[player * 64 + gang.sector] + chaos_successes[i]
for s in 0..64:
    for p in 0..6:
        let amount = total[p * 64 + s]
        if sectors[s].owner != p:
            amount = amount / 2
        cash[p] = cash[p] + amount
        cash_earned[p] = cash_earned[p] + amount
```

## Outputs

No return value. Raises each player's `cash` and `cash_earned` by its Chaos
payout, sector by sector and, within a sector, player by player. No draws.

## Edge cases

- Halving happens once per player and sector, after the gangs' successes are
  added, so two gangs with 1 success each earn 1 cash outside the player's own
  sectors, where halving each gang separately would earn 0.
- A sector that cracked down this turn pays nothing, since RULE-CHAOS-001 set
  its gangs' successes to 0.
- A Chaos gang that died in this turn's combat has `sector` set to
  `GANG_INACTIVE` by then and is not paid [FND-CHAOS-002].
- A gang is paid for the sector it is in after combat, its ordered sector,
  since Move comes later.
- No test of police presence is made: a sector under presence that did not
  crack down this turn pays in full [FND-CHAOS-002].
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

None known.
