---
id: RULE-AI-016
title: Every Attack order lowers the target player's attitude toward the attacker by the larger of its reaction and the opening damage
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-047, FND-AI-006, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-SETUP-004, RULE-ATTACK-001]
---

## Summary

A player whose gang is attacked holds a grudge against the attacker. Its
attitude toward the attacker drops by its reaction value or by the damage the
opening attack dealt, whichever is larger, down to no lower than -10. It
happens once for every gang whose action is Attack, whether the attack was
evaded, hit, or drew a retaliation.

## When it runs

During `resolution`, at the end of each attacking gang's step in the attack
block. RULE-ATTACK-001 calls `grudge_after_attack` once per gang whose action
is Attack, with the player in the gang's target and its `opening_damage` (-1
for an evaded attack). The retaliation makes no call.

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
permanent. An evaded attack or one that deals no damage still lowers the
attitude by the reaction, which is at least 3 at the other Mentality settings, so any attack
on a gang of a player whose attitude is below 3 makes that player hostile.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' attitudes.

## Differences between builds

None known.

An attack on a gang of the attacker's own player lowers that player's
attitude toward itself.

## Open questions

None.
