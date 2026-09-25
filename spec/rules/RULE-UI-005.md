---
id: RULE-UI-005
title: Lengths of the site progress and Force meters
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-036, FND-UI-010]
conflicting: []
split_with: []
related: []
---

## Summary

A site's progress meter is 100 pixels long at completion and shows the whole
percent of the site's Resistance overcome so far. A gang's Force meter grows by
six pixels per point of Force.

## When it runs

When the detailed sector screen draws its sites and gang cards.

## Parameters

None.

## Inputs

None.

## Procedure

```text
define site_meter_length(progress, resistance) -> INT32:
    if resistance == 0:
        return 100
    return progress * 100 / resistance

define force_meter_length(force) -> INT32:
    return force * 6
```

## Outputs

`site_meter_length` gives the number of pixels copied from the 100-by-3 green
strip at `(354,0)` of `PX00129` over the site's red track, and
`force_meter_length` the number copied over a gang card's 60-by-3 red track.

## Edge cases

- The division truncates: a site at 2 of 3 shows 66 pixels.
- A site with a Resistance of 0, such as a headquarters, shows a full meter.

## What the sources say

SRC-MANUAL-GOG, page 21 (Gangs), describes a Force bar on each gang card and
does not say how it is scaled.

## Differences between builds

None known.

## Open questions

- What a Force above 10, or progress above the Resistance, draws beyond the
  track.
