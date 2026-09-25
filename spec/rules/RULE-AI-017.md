---
id: RULE-AI-017
title: A Control takeover lowers the previous owner's attitude toward the new owner by twice its reaction
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-047, FND-CONTROL-003, FND-AI-006, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-SETUP-004, RULE-CONTROL-001]
---

## Summary

A player that loses a sector to another player's Control holds a grudge
against the new owner. Its attitude toward the new owner drops by twice its reaction value,
down to no lower than -10.

## When it runs

During `resolution`, when Control gives a sector a new owner. RULE-CONTROL-001
calls `grudge_after_takeover` once per takeover of a sector that had an owner,
after raising the winner's overthrow count and before writing the new owner.
No other writer of a sector's owner calls it, so a sector lost to a Crackdown
or in any other way changes no attitude.

## Parameters

None.

## Inputs

`attitude` and `reaction`.

## Procedure

```text
define grudge_after_takeover(previous_owner, new_owner):
    if previous_owner < 0 or new_owner < 0:
        return
    let cell = previous_owner * 6 + new_owner
    attitude[cell] = max(attitude[cell] - 2 * reaction[previous_owner], -10)
    return
```

## Outputs

Lowers one `attitude` cell, clamped at -10. Returns nothing. Makes no draw.

## Edge cases

At Homicidal Maniac every reaction is 0, so a lost sector changes nothing. At
the other settings the drop is 6 to 12, since a reaction is 3 to 6, so a
player with an attitude of 0 turns hostile after one lost sector.

## What the sources say

SRC-MANUAL-GOG does not describe the computer players' attitudes.

## Differences between builds

None known.

## Open questions

None. The test of `new_owner` in the procedure never fails, since the winner
of a takeover is a player.
