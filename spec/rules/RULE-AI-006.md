---
id: RULE-AI-006
title: The shared AI sector selector scores the nearest sectors by mode and routes one step toward the best
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-005, FND-AI-025, FND-AI-026, FND-AI-027, FND-AI-028, FND-AI-040, FND-AI-006, FND-AI-013, FND-EXE-004, FND-AI-056, FND-STATE-004, FND-AI-052, FND-AI-066, EXP-TURN-004, EXP-TURN-006]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-007, RULE-RNG-002, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

When a computer gang moves, its handler asks this selector for a destination.
The selector gives each sector near the gang a score according to what the
handler wants (free land, own land, enemy land, sites, the leader, an
objective), keeps the nearest ring of sectors that score anything, and sorts
the scores into a list it keeps from one call to the next. It picks the best at
random among ties and returns it when it is next to the gang, or else the first
step toward it that does not put more than six of the player's gangs in one
sector. The list keeps old scores for the sectors a late filter clears, and the
tie count can run past its end, so a pick can land on a sector no score of this
call points to.

## When it runs

Whenever a family handler, the dispatcher or the Move-capacity repair calls
`select_sector`, during `planning_phase` or `move_phase`.

## Parameters

None.

## Inputs

`gangs`, `sectors`, `site_definitions`, `planning_records` (the acting
record's `family`, each record's `previous_action`, and the bytes of every
record for a tie count that runs past the table), `selector_pairs`, `aux_records`
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

# Selector 0x2D: searches the standings bytes for the values p and q as if
# they were player slots in ranking order
define standings_test(p, q):
    if q == -1 or q == p:
        return false
    let i = 6
    for k in 0..6:
        if scenario_standing[k] == p:
            i = k
            break
    let j = 6
    for k in 0..6:
        if scenario_standing[k] == q:
            j = k
            break
    return j < i or i == 0

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
    else if mode == 4:
        if standings_test(player, owner_query(c)):
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
    # the late filters; a filtered sector of family 0 or 1 keeps its old pair
    let fam = planning_records[idx].family
    for c in 0..64:
        if sectors[c].crackdown_turns != 0:
            score[c] = 0
        if (fam == 0 or fam == 1) and not solo_control_ok(player, idx, c) and sectors[c].owner != player:
            score[c] = 0
        else:
            selector_pairs[c].score = score[c]
    # the sort: an exchange sort, highest score first
    for i in 0..64:
        selector_pairs[i].sector = i
    for i in 0..64:
        for j in i..64:
            if selector_pairs[i].score < selector_pairs[j].score:
                let kept = selector_pairs[i]
                selector_pairs[i] = selector_pairs[j]
                selector_pairs[j] = kept
    let top = selector_pairs[0].score
    let first = selector_pairs[0].sector
    let n = 1
    while pair_score(n) == top:
        n = n + 1
    let target = first
    if n > 1:
        target = pair_sector(roll(n) - 1)
    if abs(first % 8 - sx) <= 1 and abs(first / 8 - sy) <= 1 and top > 0:
        return target
    # routing uses signed % and /, which truncate toward zero
    let tx = target % 8
    let ty = target / 8
    let result = src
    if sx < tx:
        result = result + 1
        if sector_gang_count[player * 64 + result] > 5:
            result = result - 1
    if sx > tx:
        result = result - 1
        if sector_gang_count[player * 64 + result] > 5:
            result = result + 1
    if sy < ty:
        result = result + 8
        if sector_gang_count[player * 64 + result] > 5:
            result = result - 8
    if sy > ty:
        result = result - 8
        if sector_gang_count[player * 64 + result] > 5:
            result = result + 8
    return result

# The memory from selector_pairs on, read as pairs of INT32: the 64 pairs, then
# score, which the selector keeps as 64 INT32 whose element x * 8 + y is the
# score of sector y * 8 + x, then the bytes of planning_records
define pair_dword(k):
    if k < 128:
        if k % 2 == 0:
            return selector_pairs[k / 2].score
        return selector_pairs[k / 2].sector
    if k < 192:
        let m = k - 128
        return score[(m % 8) * 8 + m / 8]
    return record_dword(k - 192)

# Dword k of planning_records as the game holds them, 16 bytes per record in
# element order, read as a little-endian INT32
define record_dword(k):
    let r = planning_records[k / 4]
    let v = 0
    if k % 4 == 3:
        v = (r.weapon_cooldown + 65536) % 65536 + ((r.armor_cooldown + 65536) % 65536) * 65536
    else:
        let b = [r.family, r.needs_family, r.older_action, r.older_target]
        if k % 4 == 1:
            b = [r.older_target_2, r.previous_action, r.previous_target, r.previous_target_2]
        if k % 4 == 2:
            b = [r.planned_action, r.planned_target, r.planned_target_2, r.unk_0B]
        let weight = 1
        for i in 0..4:
            v = v + ((b[i] + 256) % 256) * weight
            weight = weight * 256
    if v >= 2147483648:
        v = v - 4294967296
    return v

define pair_score(n):
    return pair_dword(n * 2)

define pair_sector(n):
    return pair_dword(n * 2 + 1)
```

## Outputs

Returns the destination sector: a sector the tie draw picked, when the first
sorted pair is next to the gang and scores above 0, or else a sector at most
one step from the gang, its own sector when the routing steps are blocked or
the target is its own sector. Makes one `roll` when the tie count is above 1,
and none otherwise. Leaves `selector_pairs` sorted.

## Edge cases

No direct call passes mode 4 (FND-AI-028), so its scoring is written out for
completeness. It compares where the player and the owner query first appear
among the standings bytes, which hold places, so it does not compare the two
players' standings (FND-STATE-004).

When every sector scores 0 after the filters and every pair holds 0, the tie
count runs over the 64 pairs, the 32 pairs of the score table and the zero
bytes of the planning records that follow. A player's records are all zero
bytes until its first planning pass, so with a human in slot 0 the count
reaches 258 (FND-AI-066, EXP-TURN-006). A pick among the first 64 pairs is the
sector of that number, since equal scores are never swapped; a pick in the
table reads a sector field of 0, and a pick in the records reads four of their
bytes as a sector. The gang then steps toward that target. Encoded modes and
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

- Whether each radius clears the scores before rescanning is not recorded; the
  procedure writes each score by assignment, which gives the same result.
- The late filter clears sectors whose byte at sector record +15 is nonzero;
  that byte is taken to be `crackdown_turns`.
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
- A tie count that runs through the records of all six players would continue
  into `aux_records`; the procedure does not model it, and no run has reached
  it.
- A routing step off the board reads `sector_gang_count` of the neighbouring
  player, or memory outside it for players 0 and 5, and a target read from the
  planning records can be anywhere. No run has recorded such a step.
- `selector_pairs` is not cleared when a match starts or is loaded, so a second
  match in the same run of the game, or a loaded one, starts with the scores
  the last call left. The runs so far each started one new match.
