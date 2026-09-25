---
id: RULE-AI-017
title: A sector changing owner lowers the previous owner's attitude toward the new owner by twice its reaction
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006]
conflicting: []
split_with: []
related: [RULE-SETUP-004]
---

## Summary

A player that loses a sector to another player holds a grudge against the new
owner. Its attitude toward the new owner drops by twice its reaction value,
down to no lower than -10.

## When it runs

During `resolution`, when a sector's owner changes. The rules that change a
sector's owner call `grudge_after_takeover` once per change.

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

- Which resolver paths make the call (Control, Bribe, Terminate or losses of
  control) is not given with instruction addresses.
- Whether a sector that was neutral before, or becomes neutral, skips the call
  is not recorded; the procedure skips both, since there is no attitude row or
  column for a neutral owner.
