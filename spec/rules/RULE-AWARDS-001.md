---
id: RULE-AWARDS-001
title: The endgame awards go to every player tied at the extreme of each statistic, with activity thresholds for the first three
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AWARDS-001, FND-AWARDS-002, FND-COMBAT-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

At the end of a match five awards are handed out, in this order: Fist for the
most Overthrows (at least 5), Skull for the most damage dealt by the player's
own attacks (at least 50), Big Fat Chicken for the most Hide actions (at
least 10), Dollar Sign for the most cash spent, and Safe for the least cash
spent. Every player tied for an award gets it, eliminated players included.

## When it runs

Once, when the match has ended and the endgame screen is prepared, in the
final awards controller.

## Parameters

None.

## Inputs

`overthrow_count`, `damage_inflicted`, `hide_count`, `cash_spent`.

## Procedure

```text
define award_most(stat: INT32[6], start, category):
    let best = start
    for p in 0..6:
        if stat[p] > best:
            best = stat[p]
    for p in 0..6:
        if stat[p] == best:
            append(player_awards[p], category)
    return

define award_least(stat: INT32[6], start, category):
    let best = start
    for p in 0..6:
        if stat[p] < best:
            best = stat[p]
    for p in 0..6:
        if stat[p] == best:
            append(player_awards[p], category)
    return

# Categories in the builder's order: 0 Fist, 1 Skull, 2 Big Fat Chicken,
# 3 Dollar Sign, 4 Safe
award_most(overthrow_count, 5, 0)
award_most(damage_inflicted, 50, 1)
award_most(hide_count, 10, 2)
award_most(cash_spent, 0, 3)
award_least(cash_spent, 999999, 4)
```

## Outputs

No return value. Appends to `player_awards` of each winning player, in
category order. Changes nothing else and makes no draws.

## Edge cases

A value equal to the threshold wins when no one has more, since the running
maximum starts at the threshold and the second pass tests for equality. When
no one reaches a threshold, no one gets that award. When every player has
spent nothing, all six get both Dollar Sign and Safe. A player who has spent
999,999 or more cannot get Safe, and if every player has, no one does. Every
Hide the resolver carries out counts, including a recurring Hide that keeps a
gang hidden turn after turn [FND-AWARDS-002]. Retaliation does not count
toward `damage_inflicted` [FND-COMBAT-003]. A player can earn all five awards,
but the endgame screen draws only the first three (BUG-AWARDS-001).

## What the sources say

SRC-MANUAL-GOG, page 46, names the five awards and what each rewards, and says
that no combat or hiding award is given when no significant combat or hiding
happened; it gives no thresholds. Page 47 says retaliation is not counted in
Damage Inflicted. The executable's thresholds are 5, 50 and 10.

## Differences between builds

None known.

## Open questions

- Where `player_awards` is kept and the codes it stores for each category are
  not recorded; the category numbers here are the builder's order.
- Whether the table is cleared before the builder runs is not recorded.
