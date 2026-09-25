---
id: RULE-UI-012
title: Objective sectors marked on the city map
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-033, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

In Siege the city map marks each player's headquarters sector with two gray
pylons, and in Big Man it marks the four centre sectors the same way.

## When it runs

When the city map is drawn, after the ownership layers.

## Parameters

- `sector_number` (`INT32`): the sector drawn.

## Inputs

`scenario`, `hq_sectors`.

## Procedure

```text
if scenario == 6:
    for k in 0..6:
        if hq_sectors[k] == sector_number:
            return 1
if scenario == 8:
    if sector_number == 27 or sector_number == 28 or sector_number == 35 or sector_number == 36:
        return 1
return 0
```

## Outputs

Returns 1 when the sector gets the pylon marker: the crop `(344,15,54,52)` of
`PX00129`, keyed on exact white, over the whole 54-by-52 city cell at
`(2 + 4 + 53*column, 44 + 3 + 51*row)` on screen.

## Edge cases

In Siege every headquarters sector is marked, whoever owns it now.

## What the sources say

SRC-MANUAL-GOG, page 20 (City View), says two gray pylons mark a sector of
importance in the scenario. Page 14 describes Siege's headquarters and Big Man's
centre sectors. They agree with the executable.

## Differences between builds

None known.

## Open questions

None known.
