---
id: RULE-BRIBE-001
title: Bribe pays 3 cash to raise the gang's sector Tolerance by 3
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-BRIBE-001, FND-TURN-001, FND-EVENT-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002]
---

## Summary

A bribing gang pays 3 cash, if its player has it at that moment, and raises
its sector's Tolerance by 3. Without the cash nothing happens and the player
gets an insufficient-cash report.

## When it runs

During `instant_phase`, for each gang whose `action` is `ACTION_BRIBE`, at
that gang's place in the phase's player and roster order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the acting gang, FMT-STATE-001.

## Inputs

`cash[player]`, `cash_spent[player]`, the gang's `sector`, and that sector's
`tolerance`.

## Procedure

```text
if cash[player] < 3:
    emit BribeCashShort(player, gang.sector)
    return
cash[player] = cash[player] - 3
sectors[gang.sector].tolerance = sectors[gang.sector].tolerance + 3
cash_spent[player] = cash_spent[player] + 3
```

## Outputs

No return value. On success, subtracts 3 from `cash[player]`, adds 3 to the
sector's `tolerance` and adds 3 to `cash_spent[player]`, in that order. On
failure, emits `BribeCashShort` and changes nothing. Makes no random draw.

## Edge cases

- A player with exactly 3 cash can bribe.
- No cap applies: Tolerance can pass 40, and several Bribes in one sector in
  one turn each add 3.
- The cash test uses cash as it stands when the gang acts, after the Bribes
  of earlier gangs in the phase's order.
- `tolerance` is a signed byte; a Tolerance above 124 would wrap to a negative
  value, if the addition is made at byte width (see Open questions).

## What the sources say

SRC-MANUAL-GOG, page 29, describes Bribe as paying the officials to raise a
sector's Tolerance for a while, with Tolerance moving back toward normal by one
point each turn. Page 50 gives the cost as 5 cash for 3 points and a maximum
base Tolerance of 40. Page 18 says a player in debt cannot give Bribe orders.
The executable charges 3, not 5, and applies no maximum. See BUG-BRIBE-001.

## Differences between builds

None known.

## Open questions

- The instruction addresses of the Bribe case in `0x00472775` are not
  recorded, nor whether the Tolerance addition is made at byte width.
- Whether the Bribe case also marks the sector as changed, as the Snitch case
  does, is not recorded.
- That `tolerance` at offset `0x05` of FMT-STATE-002 is the byte the Bribe case
  writes is assumed; the finding does not give the offset.
- The report itself is RULE-EVENT-008, the handler of `BribeCashShort`.
