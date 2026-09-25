---
id: RULE-EQUIP-002
title: The transaction pass carries out Equip, Give and Sell by player and roster slot, and delivers gifts after each player's scan
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EQUIP-002, FND-EQUIP-006, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EQUIP-001, RULE-GIVE-001, RULE-SELL-001, FMT-STATE-001]
---

## Summary

Equipment orders are carried out one player after another and, for each
player, in the fixed order of the gangs' roster slots. Purchases and sales
change cash as each gang's turn comes. Items given away reach their recipients
only after all of that player's gangs have been handled.

## When it runs

`transaction_phase`, after `combat_phase` and before `chaos_payout_phase`.

## Parameters

None.

## Inputs

`turn_order`, `gangs` with each gang's `sector`, `action`, `weapon`, `armor`
and `misc`.

## Procedure

```text
for each player in turn_order:
    let pending_weapon: INT8[] = []
    let pending_armor: INT8[] = []
    let pending_misc: INT8[] = []
    for slot in 0..81:
        append(pending_weapon, -1)
        append(pending_armor, -1)
        append(pending_misc, -1)
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        # the active test is assumed; see Open questions
        if gang.sector == GANG_INACTIVE:
            continue
        if gang.action == ACTION_EQUIP:
            call RULE-EQUIP-001(player, gang)
        else if gang.action == ACTION_GIVE:
            call RULE-GIVE-001(gang, pending_weapon, pending_armor, pending_misc)
        else if gang.action == ACTION_SELL:
            call RULE-SELL-001(player, gang)
    for slot in 0..81:
        let recipient = gangs[player * 81 + slot]
        if pending_weapon[slot] != -1:
            recipient.weapon = pending_weapon[slot]
        if pending_armor[slot] != -1:
            recipient.armor = pending_armor[slot]
        if pending_misc[slot] != -1:
            recipient.misc = pending_misc[slot]
```

## Outputs

No return value. The changes are those of RULE-EQUIP-001, RULE-GIVE-001 and
RULE-SELL-001, in the order above, followed by each player's deliveries. Makes
no random draw.

## Edge cases

- The order the player gave the orders in plays no part.
- A Sell by an earlier roster slot can pay for an Equip by a later one; a Sell
  by a later slot, the Chaos payout and the next Upkeep cannot
  (FND-EQUIP-006).
- A gift is delivered after the recipient's own transaction, so it replaces an
  item the recipient bought this turn, or kept, in the same slot. That item is
  lost.
- Two gifts to the same slot of the same recipient: the later roster slot's
  gift is delivered and the earlier one is lost, since its giver's slot has
  already been emptied.
- Two gangs can swap items in the same slot by giving to each other: both
  givers' slots are emptied first and both deliveries follow.

## What the sources say

SRC-MANUAL-GOG, page 45 (Command Sequence), puts Equip, Give and Sell in the
Transaction phase after Combat, and its designer's note says that when the
transacting gang is killed, the items involved are lost. The manual gives no
order among the transactions.

## Differences between builds

None known.

## Open questions

- FND-EQUIP-002 does not record whether the scan skips inactive records. The
  procedure assumes it does, which fits the manual's note that the items of a
  killed transacting gang are lost.
- Whether a delivery tests that the recipient is still active is not recorded.
