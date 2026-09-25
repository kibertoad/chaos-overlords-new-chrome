---
id: RULE-TOLERANCE-002
title: After the instant phase every sector's Tolerance below 1 is raised to 1
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SNITCH-001, FND-TURN-001, FND-CONTROL-001]
conflicting: []
split_with: []
related: [FMT-STATE-002]
---

## Summary

Once every Bribe, Snitch and other instant action has been carried out, any
sector whose Tolerance has fallen below 1 is set to 1, so no sector enters the
rest of the turn with a Tolerance of 0 or less.

## When it runs

At the end of `instant_phase`, after the last gang's instant action and
before `chaos_phase`.

## Parameters

None.

## Inputs

Each sector's `tolerance`.

## Procedure

```text
for each sector in sectors:
    if sector.tolerance < 1:
        sector.tolerance = 1
```

## Outputs

No return value. Sets `tolerance` to 1 in every sector where it was below 1.
Makes no random draw.

## Edge cases

- The floor is applied once, after all instant actions, so a Snitch and a
  Bribe in the same sector in one turn net out before it: Tolerance 2, then a
  Snitch to -1 and a Bribe to 2, ends at 2.
- The loop tests every sector, so a sector that was below 1 before the phase
  began is raised as well, even when no gang acted in it.

## What the sources say

SRC-MANUAL-GOG, pages 50 and 51, gives Bribe a maximum and Snitch a minimum of
the base Tolerance, and says a sector with a negative Tolerance will have
crackdowns. The executable has neither per-command limit and applies this
single floor of 1 instead.

## Differences between builds

None known.

## Open questions

- The instruction addresses of the loop in `0x00472775` are not recorded, nor
  the order it visits the sectors in; the order does not change the result.
- That `tolerance` at offset `0x05` of FMT-STATE-002 is the byte the loop
  reads is assumed.
