---
id: RULE-AI-016
title: An attack lowers the defender's attitude toward the attacker by the larger of its reaction and the damage
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006]
conflicting: []
split_with: []
related: [RULE-SETUP-004]
---

## Summary

A player whose gang is attacked holds a grudge against the attacker. Its
attitude toward the attacker drops by its reaction value or by the damage the
attack dealt, whichever is larger, down to no lower than -10.

## When it runs

During `resolution`, where an attack's damage is known. The combat rules call
`grudge_after_attack` once per attack.

## Parameters

None.

## Inputs

`attitude` and `reaction`.

## Procedure

```text
define grudge_after_attack(attacker, defender, damage):
    let cell = defender * 6 + attacker
    attitude[cell] = max(attitude[cell] - max(reaction[defender], damage), -10)
    return
```

## Outputs

Lowers one `attitude` cell, clamped at -10. Returns nothing. Makes no draw.

## Edge cases

At Homicidal Maniac every reaction is 0, so an attack lowers the attitude by
the damage alone, and since attitudes do not recover there the drop is
permanent. An attack that deals no damage still lowers the attitude by the
reaction, which is at least 3 at the other Mentality settings, so any attack
on a gang of a player whose attitude is below 3 makes that player hostile.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' attitudes.

## Differences between builds

None known.

## Open questions

- Which resolver loop makes the call, and whether it runs once per attack or
  once per attacking gang, is not given with an instruction address.
- That the reaction used is the defender's is assumed (FND-AI-006).
- Whether an attack between two gangs of one player, or retaliation damage,
  also calls it is not recorded.
