---
id: RULE-AI-018
title: A new match gives computer players difficulty band 0 at Goon, 1 at Criminal and 2 at Crime Lord and Homicidal Maniac
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-007, FND-AI-004]
conflicting: []
split_with: []
related: []
---

## Summary

The Mentality setting changes the computer players' dice, not only their
planning. Each player has a difficulty band. Human players are always in band
1. Computer players are in band 0 at Goon, where several of their rolls get
worse, band 1 at Criminal, where they roll like humans, and band 2 at Crime
Lord and Homicidal Maniac, where several rolls get better.

## When it runs

During new-match set-up.

## Parameters

None.

## Inputs

`mentality` and `controller`.

## Procedure

```text
for p in 0..6:
    difficulty_band[p] = 1
    if controller[p] == 1:
        if mentality == 0:
            difficulty_band[p] = 0
        else if mentality >= 2:
            difficulty_band[p] = 2
```

## Outputs

Sets all six entries of `difficulty_band`. Makes no draw. The band is then
read during `resolution` by the Heal, Influence, Research, Chaos, hidden-target,
attack and retaliation rolls, whose rules own those reads (FND-AI-007): band 0
removes a fifth of some pools or needs higher dice, band 2 needs lower dice,
and a band-2 owner's sector counts only three quarters of the Chaos successes
toward a Crackdown.

## Edge cases

A player slot that is empty (controller -1) stays in band 1. A network human
(controller 3) stays in band 1.

## What the sources say

SRC-MANUAL-GOG, numbered pages 14 and 15, calls the Mentality settings
difficulty levels: Goon the easiest, Criminal normal, Crime Lord very hard and
Homicidal Maniac extremely hard. It gives no numbers.

## Differences between builds

None known.

## Open questions

- Which controller values count as computer players is taken to be controller
  1 only; FND-AI-007 says "computer-controlled".
- The instruction addresses of the nine reads in the resolver are not recorded.
