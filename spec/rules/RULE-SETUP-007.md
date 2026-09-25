---
id: RULE-SETUP-007
title: A player named with the visibility modifier sees every opposing gang for the whole match
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-011]
conflicting: []
split_with: []
related: [RULE-SETUP-001, RULE-DETECT-001]
---

## Summary

A player whose name is exactly the executable's visibility modifier name gets
a flag that makes the detection pass treat every sector as searched with an
overwhelming Detect, so every opposing gang is visible to that player.

## When it runs

Once per new match, inside RULE-SETUP-004. The flag is then read by every run
of RULE-DETECT-001.

## Parameters

None.

## Inputs

`player_names`, `modifier_name_visibility`.

## Procedure

```text
for each player in turn_order:
    if name_matches(player, modifier_name_visibility):
        modifier_visibility[player] = 1
```

## Outputs

No return value. Sets `modifier_visibility` of each matching player. RULE-DETECT-001
then starts that player's sector detection values at 1000 instead of -32000,
so every opposing gang with Stealth of 1000 or less is visible. Makes no draws.

## Edge cases

The flag is saved and loaded with the match, so it lasts after a reload.

## What the sources say

None of the sources mentions the modifier.

## Differences between builds

None known.

## Open questions

- The address of the string `modifier_name_visibility` is not recorded.
- Where `modifier_visibility` is cleared for a player without the modifier
  (at a fresh setup, as the other modifier flags are) is not recorded.
