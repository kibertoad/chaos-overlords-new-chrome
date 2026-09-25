---
id: RULE-AI-014
title: A new match starts every attitude at 0, or at Homicidal Maniac at -10 toward humans and +10 toward computers
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006, FND-AI-004]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-SETUP-004]
---

## Summary

Each player has an attitude toward each other player, from -10 to +10, and a
negative attitude makes the computer players treat that player as an enemy. A
new match starts every attitude at 0. At Homicidal Maniac every player instead
starts hostile to every human player (-10) and friendly to every computer
player (+10).

## When it runs

During new-match set-up, in the initializer that also draws each player's
`reaction` (RULE-SETUP-004), before the city is generated.

## Parameters

None.

## Inputs

`mentality` and `controller`.

## Procedure

```text
for observer in 0..6:
    for other in 0..6:
        let value = 0
        if mentality == 3:
            if is_human(other):
                value = -10
            else:
                value = 10
        attitude[observer * 6 + other] = value
```

## Outputs

Sets all 36 cells of `attitude`. Makes no draw.

## Edge cases

At Homicidal Maniac a human player's own row is filled too, and so is each
player's cell toward itself: -10 for a human, +10 for a computer. Neither is
read as hostility toward oneself, since the queries skip the observing player.
A player slot whose controller is empty counts as a computer here.

## What the sources say

SRC-MANUAL-GOG, numbered pages 14 and 15, orders the Mentality settings by
difficulty and calls Homicidal Maniac extremely difficult; it does not mention
attitudes.

## Differences between builds

None known.

## Open questions

- Whether the attitude cells are filled before or after the reaction draws in
  the same initializer is not recorded; neither step reads the other's result.
- Whether the cells a player holds toward itself are written, or skipped, is
  not recorded; the procedure writes all 36.
