---
id: RULE-BRIBE-001
title: Bribe pays 3 cash to raise the gang's sector base Tolerance by 3
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-BRIBE-001, FND-TOLERANCE-001, FND-TURN-007, FND-TURN-001, FND-STATE-001, FND-EVENT-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002, RULE-TOLERANCE-001, RULE-TOLERANCE-002]
---

## Summary

A bribing gang pays 3 cash, if its player has it at that moment, and raises
its sector's base Tolerance by 3. Without the cash nothing happens and the
player gets an insufficient-cash report. The raised base reaches the
Tolerance that the Chaos test reads only when the sector record is rebuilt
before the next planning phase.

## When it runs

During `instant_phase`, for each gang whose `action` is `ACTION_BRIBE`, at
that gang's place in the phase's player and roster order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the acting gang, FMT-STATE-001.

## Inputs

`cash[player]`, `cash_spent[player]`, the gang's `sector`, and that sector's
`base_tolerance`.

## Procedure

```text
if cash[player] < 3:
    emit BribeCashShort(player, gang.sector)
    return
cash[player] = cash[player] - 3
let s = sectors[gang.sector]
let raised = s.base_tolerance + 3
if raised > 127:
    raised = raised - 256    # the store keeps the low byte
s.base_tolerance = raised
cash_spent[player] = cash_spent[player] + 3
```

## Outputs

No return value. On success, subtracts 3 from `cash[player]`, adds 3 to the
sector's `base_tolerance` and adds 3 to `cash_spent[player]`, in that order,
and marks the sector for the network update (FND-TOLERANCE-001). On failure,
emits `BribeCashShort` and changes nothing. Makes no random draw.

## Edge cases

- A player with exactly 3 cash can bribe.
- The case has no cap; RULE-TOLERANCE-002 clamps the base Tolerance to 40
  after the whole phase, so several Bribes in one sector in one turn each add 3
  and the excess above 40 is then removed.
- The cash test uses cash as it stands when the gang acts, after the Bribes
  of earlier gangs in the phase's order.
- The Chaos test of the same turn compares with `tolerance`, which was rebuilt
  before planning; a Bribe first protects the sector in the next turn's Chaos
  test.
- The base Tolerance is at most 40 when the phase begins, so the signed byte
  wraps only on the thirtieth Bribe in one sector in one turn, which takes it
  from 127 to -126; the clamp then sets it to 1.

## What the sources say

SRC-MANUAL-GOG, page 29, describes Bribe as paying the officials to raise a
sector's Tolerance for a while, with Tolerance moving back toward normal by one
point each turn. Page 50 gives the cost as 5 cash for 3 points and a maximum
base Tolerance of 40. Page 18 says a player in debt cannot give Bribe orders.
The executable charges 3, not 5 (BUG-BRIBE-001), and applies the maximum of
40 after the phase.

## Differences between builds

None known.

## Open questions

- The report itself is RULE-EVENT-008, the handler of `BribeCashShort`.
