---
id: RULE-AI-004
title: Queries the computer players' handlers share
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-004, FND-AI-006, FND-AI-013, FND-AI-019, FND-AI-001, FND-AI-033, FND-AI-026, FND-AI-009, FND-AI-039, FND-AI-048, FND-AI-052, FND-EXE-004, FND-AI-057]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002, FMT-STATE-004, RULE-RNG-002]
---

## Summary

The computer players ask a fixed set of questions about the board: is a player
human, can a gang see an enemy in a sector and how dangerous is it, could a
gang take a sector by Control on its own, would an attack on a gang go well.
This rule defines those questions once, as functions the family handlers call.

## When it runs

Whenever a rule that plans a computer player's orders calls one of its
functions, during `planning_phase`.

## Parameters

None.

## Inputs

`controller`, `casualties`, `attitude`, `gangs` (each gang's `player`,
`sector`, `force`, `combat`, `defense`, `control`, `heal` and `visible_to`),
`sectors` (each sector's `owner`, `income`, `support`, `crackdown_turns` and
`sites`), `site_definitions`, `planning_records`, `turn_limit`,
`elapsed_turns`, and `rng_state` through `roll`.

## Procedure

```text
# A player a human plays, at this computer or over the network
define is_human(p):
    return controller[p] == 0 or controller[p] == 3

# The turns left in the match, the value the handlers call turns remaining
define turns_remaining():
    return turn_limit - elapsed_turns

# Write a planned action into the planning record and into the gang record
define plan(idx, action, t1, t2):
    let r = planning_records[idx]
    r.planned_action = action
    r.planned_target = t1
    r.planned_target_2 = t2
    let g = gangs[idx]
    g.action = action
    g.target = t1
    g.target_2 = t2
    return

# How dangerous the first gang the observer can see in sector s is:
# 10 for a gang of a human the observer is hostile to, 1 for any other, 0 for none
define visible_weight(observer, s):
    for p in 0..6:
        if p == observer:
            continue
        for slot in 0..81:
            let g = gangs[p * 81 + slot]
            if g.sector == s and g.visible_to[observer] != 0:
                if is_human(p) and attitude[observer * 6 + p] < 0:
                    return 10
                return 1
    return 0

# Whether a Crackdown is in force in sector s
define crackdown_in_force(s):
    return sectors[s].crackdown_turns != 0

# Whether the gang idx could take sector s by Control on its own
define solo_control_ok(player, idx, s):
    let sec = sectors[s]
    # owner values -2 and -3 are rejected too; no rule writes them
    if sec.crackdown_turns != 0:
        return false
    if sec.owner == player:
        return false
    let g = gangs[idx]
    let defence = sec.income + sec.support
    if sec.owner != SECTOR_NEUTRAL:
        for p in 0..6:
            if p == player:
                continue
            for slot in 0..81:
                let d = gangs[p * 81 + slot]
                if d.sector == s and d.visible_to[player] != 0:
                    defence = defence + d.force + d.control
    return g.force + g.control > defence

# The number of the player's gangs in sector s whose previous action is action
define previous_action_count(player, s, action):
    let n = 0
    for slot in 0..81:
        let idx = player * 81 + slot
        if gangs[idx].sector == s and planning_records[idx].previous_action == action:
            n = n + 1
    return n

# The strength test before an AI attack, on the attacker a and the target t
define strength_check(a, t):
    let x = gangs[a]
    let y = gangs[t]
    return (y.force + y.combat) / 4 - x.defense <= x.force + x.combat - y.defense

# The gangs in sector s that the player can see, other players first by slot,
# then by roster slot. kind 0: every such gang; 1: those of human players;
# 2: those of players the player is hostile to; 3: those of the sector's owner
define visible_opponents(player, s, kind):
    let pool = []
    for p in 0..6:
        if p == player:
            continue
        if kind == 1 and not is_human(p):
            continue
        if kind == 2 and attitude[player * 6 + p] >= 0:
            continue
        if kind == 3 and sectors[s].owner != p:
            continue
        for slot in 0..81:
            let g = gangs[p * 81 + slot]
            if g.sector == s and g.visible_to[player] != 0:
                append(pool, p * 81 + slot)
    return pool

# The owner the handlers read: -2 under police presence, else the owner byte
define owner_query(s):
    if sectors[s].crackdown_turns != 0:
        return -2
    return sectors[s].owner

# Whether the player's attitude toward the owner query's value is negative.
# The value is used as a column without a range test: for -1 or -2 the read
# falls before the player's row
define hostile_owner(player, s):
    let i = player * 6 + owner_query(s)
    if i < 0:
        # player 0 reads bytes of modifier_visibility, which are never negative
        return false
    return attitude[i] < 0

# Whether sector s is owned by a human player the player is hostile to
define hostile_human_owner(player, s):
    let o = sectors[s].owner
    return o >= 0 and hostile_owner(player, s) and is_human(o)

# Whether the owner byte of sector s names a human (selector 0x35). The byte
# is used as an index without a range test: for a neutral sector (-1) the read
# falls on the last entry of casualties, player 5's count
define owner_is_human(s):
    let o = sectors[s].owner
    let v = 0
    if o >= 0:
        v = controller[o]
    else:
        v = casualties[5]
    return v == 0 or v == 3

# One target draw from the pool of kind; the strength test uses the gang with
# the same ordinal in the full pool. Returns the drawn target when the test
# passes, and -1 when it fails
define draw_once(player, idx, kind):
    let s = gangs[idx].sector
    let pool = visible_opponents(player, s, kind)
    let full = visible_opponents(player, s, 0)
    let k = roll(count(pool))
    if strength_check(idx, full[k - 1]):
        return pool[k - 1]
    return -1

# Up to tries target draws, stopping at the first that passes the strength
# test; returns the last drawn target whether or not it passed
define draw_target(player, idx, kind, tries):
    let s = gangs[idx].sector
    let pool = visible_opponents(player, s, kind)
    let full = visible_opponents(player, s, 0)
    let t = -1
    let n = 0
    while n < tries:
        let k = roll(count(pool))
        t = pool[k - 1]
        n = n + 1
        if strength_check(idx, full[k - 1]):
            break
    return t

# Whether the site in slot k of sector s still needs Influence
define site_unfinished(s, k):
    let site = sectors[s].sites[k]
    return site_definitions[site.definition].resistance - site.progress >= 1
```

## Outputs

`is_human`, `crackdown_in_force`, `solo_control_ok`, `strength_check` and
`site_unfinished` return true or false. `turns_remaining`, `visible_weight`
and `previous_action_count` return an integer. `visible_opponents` returns a
list of indexes into `gangs`. `plan` returns nothing; it writes the planned
action and its two target bytes into the gang's planning record and gang
record. `hostile_owner` and `hostile_human_owner` return true or false.
`draw_once` makes one `roll` and returns an index into `gangs` or -1;
`draw_target` makes one `roll` per attempt, up to `tries`, and returns the
index of the last target drawn. No other function here draws from `rng`.

## Edge cases

`visible_weight` decides on the first visible gang it finds: a visible
computer gang found before a hostile human gang gives 1. `strength_check`
divides with truncation toward zero. `solo_control_ok` is strict: equal
strength fails. The strength test in `draw_once` and `draw_target` reads the
gang at the drawn position of the full list, which is a different gang from
the one drawn whenever the pool is a narrower list (BUG-AI-003). With an empty
pool, `roll(0)` gives 1 and the draw reads the first element of an empty list;
what the original reads there is not recorded. `previous_action_count` never
counts an inactive gang, whose sector is 100.

`hostile_owner` makes no test of the owner. For the player's own sector it
reads the player's attitude toward itself. For a neutral sector or one under
police presence it reads, for players 1 to 5, the previous player's attitude
toward player 5 or player 4, and for player 0 a value that is never negative.
`solo_control_ok` counts every visible gang of another player in the sector,
the owner's and any third player's.

## What the sources say

SRC-MANUAL-GOG does not describe these queries.

## Differences between builds

None known.

## Open questions

- `crackdown_in_force` is taken to test for a nonzero Crackdown byte; a positive
  test is also possible.
- Whether `visible_weight` stops at the first visible gang or looks on for one
  that earns 10 is not certain (FND-AI-013).
- `plan` writes both records at once; some handlers may write the gang record
  only when they finish. The order of the two writes has no effect within
  planning.
- The pool of kind 3 (the owner's gangs) comes from the description of the
  family-13 and family-14 contested branch only (FND-AI-039).
