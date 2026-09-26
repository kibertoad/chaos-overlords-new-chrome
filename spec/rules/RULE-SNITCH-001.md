---
id: RULE-SNITCH-001
title: Snitch lowers the gang's sector base Tolerance by 3, free and whatever the player's cash
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SNITCH-001, FND-TOLERANCE-001, FND-TURN-007, FND-TURN-001, FND-STATE-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002, RULE-TOLERANCE-001, RULE-TOLERANCE-002]
---

## Summary

A snitching gang tips off the police and lowers its sector's base Tolerance
by 3. It costs nothing and works even when its player is in debt. The lowered
base reaches the Tolerance that the Chaos test reads only when the sector
record is rebuilt before the next planning phase.

## When it runs

During `instant_phase`, for each gang whose `action` is `ACTION_SNITCH`, at
that gang's place in the phase's player and roster order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the acting gang, FMT-STATE-001.

## Inputs

The gang's `sector` and that sector's `base_tolerance`.

## Procedure

```text
let s = sectors[gang.sector]
s.base_tolerance = s.base_tolerance - 3
```

## Outputs

No return value. Subtracts 3 from the sector's `base_tolerance` and marks the
sector for the network update (FND-TOLERANCE-001). Makes no random draw and
reads no cash.

## Edge cases

- No floor applies here: the base Tolerance can drop below 0 during the phase.
  After the whole phase, RULE-TOLERANCE-002 raises every base Tolerance below
  1 to 1.
- Several Snitches in one sector in one turn each subtract 3.
- The subtraction is made in 32 bits and stored as a signed byte. Starting
  from at least 1, the byte wraps only on the forty-fourth Snitch in one sector
  in one turn.
- The Chaos test of the same turn compares with `tolerance`, which was rebuilt
  before planning; a Snitch first raises the chance of a Crackdown in the next
  turn's Chaos test.

## What the sources say

SRC-MANUAL-GOG, page 33, describes Snitch as the opposite of Bribe, free, and
lowering the sector's Tolerance by 3 to raise the chance of a Crackdown. Page
51 gives a minimum base Tolerance of 0. Page 18 says a player in debt cannot
give Snitch orders. The executable has no per-command minimum, raises the
result to 1 after the phase instead, and does not test the player's cash.

## Differences between builds

None known.

## Open questions

- Whether the planning screens refuse a Snitch order from a player in debt, as
  the manual says, is not recorded.
