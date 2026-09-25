---
id: RULE-SETUP-005
title: A player named with the island modifier puts every neutral sector under a Crackdown that never ends
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-003, FND-SETUP-001]
conflicting: []
split_with: []
related: [RULE-SETUP-001, FMT-STATE-002]
---

## Summary

If any player's name is exactly the executable's island modifier name, every
sector no player owns at the start of the match is under a permanent
Crackdown. The six headquarters sectors are not.

## When it runs

Once per new match, inside RULE-SETUP-004, after the headquarters have owners
and the Right Hands exist.

## Parameters

None.

## Inputs

`player_names`, `modifier_name_islands`, each sector's `owner`.

## Procedure

```text
for each player in turn_order:
    if name_matches(player, modifier_name_islands):
        for s in 0..64:
            if sectors[s].owner == SECTOR_NEUTRAL:
                sectors[s].crackdown_turns = CRACKDOWN_PERMANENT
```

## Outputs

No return value. Sets `crackdown_turns` to 100 in every neutral sector when
some player's name matches. Makes no draws.

## Edge cases

Two players with the modifier name repeat the same writes with no further
effect. The end-of-turn police countdown never reduces 100, so these
Crackdowns last the whole match unless something else rewrites the byte.

## What the sources say

None of the sources mentions the modifier.

## Differences between builds

None known.

## Open questions

- The address of the string `modifier_name_islands` is not recorded.
- Whether the flag is kept per player or as a single byte is not stated
  exactly; per player is the reading used.
