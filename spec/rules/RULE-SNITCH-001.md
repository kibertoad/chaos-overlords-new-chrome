---
id: RULE-SNITCH-001
title: Snitch lowers the gang's sector Tolerance by 3, free and whatever the player's cash
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SNITCH-001, FND-TURN-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002, RULE-TOLERANCE-002]
---

## Summary

A snitching gang tips off the police and lowers its sector's Tolerance by 3.
It costs nothing and works even when its player is in debt.

## When it runs

During `instant_phase`, for each gang whose `action` is `ACTION_SNITCH`, at
that gang's place in the phase's player and roster order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the acting gang, FMT-STATE-001.

## Inputs

The gang's `sector` and that sector's `tolerance`.

## Procedure

```text
sectors[gang.sector].tolerance = sectors[gang.sector].tolerance - 3
# The case also marks the sector as changed; that mark is not identified.
```

## Outputs

No return value. Subtracts 3 from the sector's `tolerance` and marks the
sector as changed. Makes no random draw and reads no cash.

## Edge cases

- No floor applies here: Tolerance can drop below 0 during the phase. After
  the whole phase, RULE-TOLERANCE-002 raises every Tolerance below 1 to 1.
- Several Snitches in one sector in one turn each subtract 3.

## What the sources say

SRC-MANUAL-GOG, page 33, describes Snitch as the opposite of Bribe, free, and
lowering the sector's Tolerance by 3 to raise the chance of a Crackdown. Page
51 gives a minimum base Tolerance of 0. Page 18 says a player in debt cannot
give Snitch orders. The executable has no per-command minimum, raises the
result to 1 after the phase instead, and does not test the player's cash.

## Differences between builds

None known.

## Open questions

- What the "changed" mark is and what reads it are not recorded.
- The instruction addresses of the Snitch case in `0x00472775` are not
  recorded.
- That `tolerance` at offset `0x05` of FMT-STATE-002 is the byte the Snitch
  case writes is assumed.
- Whether the planning screens refuse a Snitch order from a player in debt, as
  the manual says, is not recorded.
