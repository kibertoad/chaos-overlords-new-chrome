---
id: RULE-DETECT-001
title: A player sees an enemy gang when its Stealth is at most the player's detection strength in that sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-DETECT-001, FND-DETECT-002, FND-EXE-004, FND-SETUP-011, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

In each sector, a player's detection strength is the best Detect among the
player's gangs there, plus at least 1 for each other gang of the player's in
the sector, and more for helpers with Detect above 9. The player sees every
enemy gang there whose Stealth is no higher than that strength, and always
sees its own gangs.

## When it runs

At each planning entry in `planning_phase`, from both the computer and the
human planning paths, before the player gives orders, as `fn_0046FA11`
(range in FND-EXE-004) [FND-DETECT-001, FND-DETECT-002].

## Parameters

None.

## Inputs

Each gang's `sector`, `detect` and `stealth` (effective values, rebuilt at
`turn_start`), and `modifier_visibility`.

## Procedure

```text
for observer in 0..6:
    let strength: INT32[] = []
    let base_slot: INT32[] = []
    for s in 0..64:
        if modifier_visibility[observer] != 0:
            append(strength, 1000)
        else:
            append(strength, -32000)
        append(base_slot, -1)

    # The best Detect in each sector is the base
    for slot in 0..81:
        let g = gangs[observer * 81 + slot]
        if g.sector != GANG_INACTIVE and g.detect > strength[g.sector]:
            strength[g.sector] = g.detect
            base_slot[g.sector] = slot

    # Every other gang helps
    for slot in 0..81:
        let g = gangs[observer * 81 + slot]
        if g.sector != GANG_INACTIVE and slot != base_slot[g.sector]:
            strength[g.sector] = strength[g.sector] + 1
            if g.detect > 9:
                strength[g.sector] = strength[g.sector] + (g.detect - 8) / 2

    for player in 0..6:
        for slot in 0..81:
            let g = gangs[player * 81 + slot]
            if g.sector != GANG_INACTIVE:
                if player == observer:
                    g.visible_to[observer] = 1
                else:
                    g.visible_to[observer] = g.stealth <= strength[g.sector]
```

## Outputs

No return value. Rewrites `visible_to` of every active gang for every
observer, 0 or 1, and leaves the bytes of inactive gangs as they were. Makes
no draws.

## Edge cases

- A helper with Detect 9 or less, including a negative Detect, adds exactly
  1. Detect 10 and 11 add 2, 12 and 13 add 3, and so on with no upper limit.
- In a sector where the observer has no gang, the strength stays at -32000 and
  no enemy there is seen, since Stealth is a signed byte.
- With the visibility name modifier every strength starts at 1000, no gang can
  become the base, and every one of the observer's gangs adds to 1000, so every
  enemy gang is seen.
- When two gangs share the best Detect, the first in roster order is the base.
  The total is the same either way.
- Hide is not read, so hiding does not change who sees a gang.
- All six observer slots are rebuilt, including slots of players who are out
  of the match or never joined [FND-DETECT-002].
- An inactive gang's `visible_to` bytes keep what they held when the gang
  died, left or was hired over [FND-DETECT-002].

## What the sources say

SRC-MANUAL-GOG, numbered page 52 (Stealth/Detect, in the Math of the Game),
compares an enemy's Stealth with the highest friendly Detect in its sector and
adds, for each other friendly gang, +1 for Detect 0 to 10, +2 for 11 to 12, +3
for 13 to 14, +4 for 15 to 16, +5 for 17 to 18 and +6 for 19 or more. The
executable differs at every band edge, gives helpers with negative Detect 1,
and has no cap above 19. Page 36 says that if one friendly gang can see an
enemy, all friendly gangs in the sector can attack it.

## Differences between builds

None known.

## Open questions

None known.
