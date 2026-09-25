---
id: RULE-AI-015
title: At the start of each turn's resolution every attitude below +10 rises by 1, except at Homicidal Maniac
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006]
conflicting: []
split_with: []
related: []
---

## Summary

Grudges fade. At the start of each turn's resolution every attitude below +10
rises by one point, so a player that stops being attacked is slowly forgiven.
At Homicidal Maniac attitudes never change this way.

## When it runs

At the start of `resolution`, once per turn, before its first step.

## Parameters

None.

## Inputs

`mentality` and `attitude`.

## Procedure

```text
if mentality != 3:
    for cell in 0..36:
        if attitude[cell] < 10:
            attitude[cell] = attitude[cell] + 1
```

## Outputs

Raises each `attitude` cell below +10 by 1. Makes no draw.

## Edge cases

A cell set to -10 takes ten turns without new grudges to reach 0 and become
non-hostile at the start of the tenth resolution. The cells a player holds
toward itself rise too, up to +10.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' attitudes.

## Differences between builds

None known.

## Open questions

- Where in the resolver's opening steps the loop runs relative to the other
  start-of-resolution work is not recorded.
