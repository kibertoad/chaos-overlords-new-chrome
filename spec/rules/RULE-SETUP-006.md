---
id: RULE-SETUP-006
title: A player named with either extra-gang modifier starts with five more Force-10 gangs in its headquarters
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SETUP-015, FND-SETUP-004, FND-CITY-003]
conflicting: []
split_with: []
related: [RULE-SETUP-001, FMT-STATE-001]
---

## Summary

A player whose name is exactly one of two modifier names held in the
executable starts with five more gangs beside the Right Hands, all at full
Force in the headquarters sector: five more Right Hands with no equipment, or
five gangs of definition 59 carrying a fixed weapon, armor and miscellaneous
item. It applies only in a local game.

## When it runs

Once per new match in a local game, inside RULE-SETUP-004, after RULE-CITY-004
has created the Right Hands and the modifier scan has set the flags.

## Parameters

None.

## Inputs

`modifier_right_hands`, `modifier_elite`, the Right Hands in roster slot 0 of
each player.

## Procedure

```text
for player in 0..6:
    let right_hands = modifier_right_hands[player]
    let elite = modifier_elite[player]
    if right_hands or elite:
        let hq = gangs[player * 81].sector
        for slot in 1..6:
            let g = gangs[player * 81 + slot]
            g.player = player
            g.sector = hq
            g.force = 10
            if elite:
                g.definition = 59
                g.weapon = 23
                g.armor = 37
                g.misc = 52
            else:
                g.definition = 0
                g.weapon = -1
                g.armor = -1
                g.misc = -1
```

## Outputs

No return value. Fills roster slots 1 to 5 of each matching player. Makes no
draws.

## Edge cases

A name equals at most one of the two strings, so the two openings cannot
combine. The extra gangs are ordinary gang records from then on.

## What the sources say

None of the sources mentions the modifiers.

## Differences between builds

None known.

## Open questions

- The bytes the finding does not name (the action bytes, `visible_to`, the
  effective statistics) are taken to be written as for the Right Hands; the
  effective statistics are rebuilt before the first planning anyway.
- `player`, `definition` and `force` of FMT-STATE-001 are placed from an
  outside source only.
