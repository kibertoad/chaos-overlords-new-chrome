---
id: RULE-TOLERANCE-002
title: After the instant phase every sector's base Tolerance is clamped to 1..40
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TOLERANCE-001, FND-SNITCH-001, FND-STATE-001, FND-TURN-008, FND-TURN-001, FND-CONTROL-001, FND-EXE-004]
conflicting: []
split_with: []
related: [FMT-STATE-002, RULE-TOLERANCE-001, RULE-BRIBE-001, RULE-SNITCH-001]
---

## Summary

Once every Bribe, Snitch and other instant action has been carried out, any
sector whose base Tolerance has fallen below 1 is set to 1, and any whose base
Tolerance has risen above 40 is set to 40.

## When it runs

At the end of `instant_phase`, after the last gang's instant action and
before `chaos_phase`.

## Parameters

None.

## Inputs

Each sector's `base_tolerance`.

## Procedure

```text
for each sector in sectors:
    if sector.base_tolerance < 1:
        sector.base_tolerance = 1
    if sector.base_tolerance > 40:
        sector.base_tolerance = 40
```

## Outputs

No return value. Sets `base_tolerance` to 1 in every sector where it was below
1 and to 40 in every sector where it was above 40. Makes no random draw.

## Edge cases

- The clamp is applied once, after all instant actions, so a Snitch and a
  Bribe in the same sector in one turn net out before it: base Tolerance 2,
  then a Snitch to -1 and a Bribe to 2, ends at 2.
- The loop tests every sector, so a sector that was out of range before the
  phase began is clamped as well, even when no gang acted in it.
- The clamp works on `base_tolerance`. The `tolerance` the Chaos pass of the
  same turn compares with was rebuilt before planning and is not changed here;
  it can be below 1 or above 40 when the completed sites' Tolerance is
  negative or large.
- A thirtieth Bribe in one sector in one turn wraps the signed byte to a
  negative value (RULE-BRIBE-001), and the clamp then sets it to 1.

## What the sources say

SRC-MANUAL-GOG, pages 50 and 51, gives Bribe a maximum of 40 and Snitch a
minimum of 0 for the base Tolerance, and says a sector with a negative
Tolerance will have crackdowns. The executable applies neither limit in the
commands; this single clamp after the phase gives the maximum of 40 and a
minimum of 1.

## Differences between builds

None known.

## Open questions

None known.
