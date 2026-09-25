---
id: RULE-AI-006
title: The shared AI sector selector scores the nearest sectors by mode and routes one step toward the best
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-005, FND-AI-025, FND-AI-026, FND-AI-027, FND-AI-028, FND-AI-040, FND-AI-006, FND-AI-013, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-007, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

When a computer gang moves, its handler asks this selector for a destination.
The selector gives each sector near the gang a score according to what the
handler wants (free land, own land, enemy land, sites, the leader, an
objective), keeps the nearest ring of sectors that score anything, picks the
best at random among ties, and returns the first step toward it that does not
put more than six of the player's gangs in one sector.

## When it runs

Whenever a family handler, the dispatcher or the Move-capacity repair calls
`select_sector`, during `planning_phase` or `move_phase`.

## Parameters

None.

## Inputs

`gangs`, `sectors`, `site_definitions`, `planning_records` (the acting
record's `family` and each record's `previous_action`), `aux_records`
(`focus`), `sector_gang_count`, `attitude`, `controller`,
`scenario_standing`, and `rng_state` through `roll`.

## Procedure

```text
# 1 when the slot is a leader of its block of six family-11 gangs
define is_block_leader(player, slot):
    let n = 0
    for s in 0..81:
        if planning_records[player * 81 + s].family == 11:
            if s == slot:
                return n % 6 == 0
            n = n + 1
    return false

# The focus sector of the leader of the slot's family-11 block
define block_leader_sector(player, slot):
    let n = 0
    let leader = -1
    for s in 0..81:
        if planning_records[player * 81 + s].family == 11:
            if n % 6 == 0:
                leader = s
            if s == slot:
                return aux_records[player * 81 + leader].focus
            n = n + 1
    return -1

# The one player with standing 0, or -1 when none or several have it
define unique_leader():
    let found = -1
    for p in 0..6:
        if scenario_standing[p] == 0:
            if found != -1:
                return -1
            found = p
    return found

define human_count():
    let n = 0
    for p in 0..6:
        if is_human(p):
            n = n + 1
    return n

# The score mode gives sector c for the gang idx of player
define mode_score(player, mode, idx, c):
    let g = gangs[idx]
    let o = sectors[c].owner
    let score = 0
    if mode == 1:
        if o == SECTOR_NEUTRAL and solo_control_ok(player, idx, c):
            score = 1
    else if mode == 2:
        if o == player:
            score = 1
    else if mode == 3:
        if o >= 0 and o != player:
            score = 1
    else if mode == 5:
        if o == SECTOR_NEUTRAL and solo_control_ok(player, idx, c):
            score = 5
        else if o == player and previous_action_count(player, c, ACTION_CHAOS) == 0:
            score = 2
        else if o >= 0 and o != player:
            score = 1
    else if mode == 6:
        if human_count() > 0 and o >= 0 and is_human(o) and attitude[player * 6 + o] < 0:
            score = 2
        let leader = unique_leader()
        if leader != -1 and leader != player:
            if o == leader:
                score = score + 1
        else if leader == -1:
            if o >= 0 and scenario_standing[o] == 0:
                score = score + 1
        else if o >= 0 and o != player and sector_gang_count[player * 64 + c] < 4:
            score = score + 1
    else if mode == 7 or mode == 8 or mode == 9:
        if o == player and (mode != 7 or previous_action_count(player, c, ACTION_INFLUENCE) == 0):
            for k in 0..3:
                let d = site_definitions[sectors[c].sites[k].definition]
                let v = d.support
                if mode == 8:
                    v = d.cash
                if mode == 9:
                    v = d.stealth
                if v > 0 and site_unfinished(c, k) == (mode != 9):
                    score = score + v
    else if mode == 10:
        if human_count() == 0:
            if o >= 0 and o != player:
                score = 1
        else if o >= 0 and is_human(o):
            score = 1
    else if mode == 11:
        if c == g.sector:
            score = 1
    else if mode >= 12 and mode <= 15:
        let centre = [27, 28, 35, 36]
        let hq = [9, 12, 30, 33, 51, 54]
        let listed = false
        if mode == 12 or mode == 14:
            for each x in centre:
                if x == c:
                    listed = true
        else:
            for each x in hq:
                if x == c:
                    listed = true
        if listed and sector_gang_count[player * 64 + c] < 6 and not ((mode == 12 or mode == 13) and o == player):
            score = 1
            if o >= 0 and is_human(o) and attitude[player * 6 + o] < 0:
                score = 5
    else if mode == 16:
        if c == block_leader_sector(player, idx - player * 81):
            score = 1
    else if mode >= 0x40:
        if c == mode - 0x40:
            score = 1
    # mode 4 is not written out; see Open questions
    # the common block after the mode switch
    if score > 0 and o >= 0 and is_human(o) and attitude[player * 6 + o] < 0:
        score = score * 5
    return score

# The destination for the gang idx of player; mode 0 is RULE-AI-007
define select_sector(player, mode, idx):
    if mode == 0:
        return random_neighbour(player, idx)
    let g = gangs[idx]
    let src = g.sector
    let sx = src % 8
    let sy = src / 8
    let score = []
    for c in 0..64:
        append(score, 0)
    let radius = 1
    let found = false
    while radius <= 7 and not found:
        for c in 0..64:
            if abs(c % 8 - sx) <= radius and abs(c / 8 - sy) <= radius:
                score[c] = mode_score(player, mode, idx, c)
                if score[c] > 0:
                    found = true
        radius = radius + 1
    score[src] = 0
    # late filters
    let fam = planning_records[idx].family
    for c in 0..64:
        if sectors[c].crackdown_turns != 0:
            score[c] = 0
        else if (fam == 0 or fam == 1) and score[c] > 0 and sectors[c].owner != player and not solo_control_ok(player, idx, c):
            score[c] = 0
    let top = 0
    for c in 0..64:
        top = max(top, score[c])
    let tied = []
    for c in 0..64:
        if score[c] == top:
            append(tied, c)
    let target = tied[0]
    if count(tied) > 1:
        target = tied[roll(count(tied)) - 1]
    let tx = target % 8
    let ty = target / 8
    if abs(tx - sx) <= 1 and abs(ty - sy) <= 1 and score[target] > 0:
        return target
    let x = sx
    let y = sy
    if tx > sx and sector_gang_count[player * 64 + sy * 8 + sx + 1] <= 5:
        x = sx + 1
    else if tx < sx and sector_gang_count[player * 64 + sy * 8 + sx - 1] <= 5:
        x = sx - 1
    if ty > sy and sector_gang_count[player * 64 + (sy + 1) * 8 + x] <= 5:
        y = sy + 1
    else if ty < sy and sector_gang_count[player * 64 + (sy - 1) * 8 + x] <= 5:
        y = sy - 1
    return y * 8 + x
```

## Outputs

Returns the destination sector, 0 to 63: a sector next to the gang, or its own
sector when both routing steps are blocked. Makes one `roll` when more than
one sector ties for the best score, and none otherwise.

## Edge cases

When every sector scores 0 after the filters, all 64 sectors tie at 0, one
`roll(64)` picks one of them, and the gang steps toward it. Encoded modes and
mode 11 always end that way, because the only sector they score is removed
when it is the gang's own. The search stops at the first radius where any
sector scores, even when that sector is the gang's own and is removed
afterwards. A sector already holding six of the player's gangs is never
entered by a routing step, but a directly returned adjacent target is not
tested against that limit.

## What the sources say

SRC-MANUAL-GOG does not describe how computer players choose where to move.

## Differences between builds

None known.

## Open questions

- Mode 4 scores +1 when the player-pair test (selector `0x2D`) accepts the
  sector's owner; the six-byte player-order table it compares has no address,
  so the mode is not written out. No direct call passes mode 4 (FND-AI-028).
- Whether each radius clears the scores before rescanning is not recorded; the
  procedure writes each score by assignment, which gives the same result.
- The order of tied sectors after the descending sort decides which sector a
  given draw picks. Ascending sector order is assumed.
- The late filter clears sectors whose byte at sector record +15 is nonzero;
  that byte is taken to be `crackdown_turns`.
- Whether the "adjacent and positive" test uses the 3-by-3 neighbourhood
  including diagonals is assumed.
- Whether the Crackdown filter also applies before the radius search stops is
  not recorded; the procedure applies it only afterwards.
- For mode 16 the procedure passes the acting slot; the findings say selector
  `0x77` stops at the acting slot, which is taken to be the gang being
  planned.
- The mode 6 +2 bonus is given here before the leader point, and the common
  multiply by five then applies to mode 6 as well; whether the common block
  applies to mode 6 is not stated.
- The site `definition` of an empty site slot, if one exists, is not
  described.
