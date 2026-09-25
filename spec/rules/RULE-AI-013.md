---
id: RULE-AI-013
title: A computer player keeps one hire placement sector and replaces it by fixed scans when it stops being a good base
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-010, FND-AI-019, FND-AI-040]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-005, FMT-STATE-001, FMT-STATE-002, FMT-STATE-003]
---

## Summary

Each computer player remembers one sector to place new gangs in, starting with
its Right Hands' sector. It keeps that sector while it owns it, has free land
next to it and has room there. Otherwise it picks the owned sector with the
most free land around it, or one where none of its gangs is raising Chaos, or
the one with the fewest foreign neighbours. In Big Man it always picks the
most central owned sector with room.

## When it runs

`refresh_anchor` runs in RULE-AI-001, after every gang has been dispatched and
before the hire choice. `seed_anchor` runs once per player when a new match is
set up, after the headquarters are placed.

## Parameters

None.

## Inputs

`placement_anchor`, `scenario`, `sectors` (`owner`, `crackdown_turns`),
`sector_gang_count`, `planning_records` (`previous_action`), `gangs` (the
Right Hands' `sector`) and `combat_records`.

## Procedure

```text
# The Crackdown byte the neighbourhood scans read at a linear index c from 0 to 64
define crackdown_at(c):
    # at index 64 the original reads byte 5 of the second combat record
    if c == 64:
        return combat_records[1].retaliation_taken
    return sectors[c].crackdown_turns

# Neutral cells without a Crackdown around an owned centre (selector 0x24)
define free_neighbours(player, center):
    let o = 0
    if center == -1:
        # the owner read lands before the sector list; see Open questions
        o = g_004A08C4
    else:
        o = owner_at(center)
    if o != player:
        return 0
    let n = 0
    let cx = center % 8
    for dy in -1..2:
        for dx in -1..2:
            let c = center + dy * 8 + dx
            if c < 0 or c >= 65:
                continue
            if cx + dx < 0 or cx + dx > 7:
                continue
            if owner_at(c) == SECTOR_NEUTRAL and crackdown_at(c) == 0:
                n = n + 1
    return n

# Cells around center not owned by the player (selector 0x26)
define foreign_neighbours(player, center):
    let n = 0
    let cx = center % 8
    for dy in -1..2:
        for dx in -1..2:
            let c = center + dy * 8 + dx
            if c < 0 or c >= 65:
                continue
            if cx + dx < 0 or cx + dx > 7:
                continue
            if owner_at(c) != player:
                n = n + 1
    return n

# A replacement anchor sector, or -1 (selector 0x25)
define choose_anchor(player, anchor):
    if scenario == 8:
        let near = [18, 26, 19, 27]
        let far = [9, 17, 25, 33, 10, 18, 26, 34, 11, 19, 27, 35, 12, 20, 28, 36]
        for each s in near:
            if sectors[s].owner == player and sector_gang_count[player * 64 + s] < 6:
                return s
        for each s in far:
            if sectors[s].owner == player and sector_gang_count[player * 64 + s] < 6:
                return s
        return anchor - 64
    let best = 0
    let choice = -1
    for s in 0..64:
        if sectors[s].owner == player and sector_gang_count[player * 64 + s] < 6:
            let n = free_neighbours(player, s)
            if n > best:
                best = n
                choice = s
    if choice != -1:
        return choice
    for s in 0..64:
        if sectors[s].owner == player and sector_gang_count[player * 64 + s] < 6:
            if previous_action_count(player, s, ACTION_CHAOS) == 0:
                return s
    best = 9
    for s in 0..64:
        if sectors[s].owner == player and sector_gang_count[player * 64 + s] < 6:
            let n = foreign_neighbours(player, s)
            if n != 0 and n < best:
                best = n
                choice = s
    return choice

define seed_anchor(player):
    placement_anchor[player] = gangs[player * 81].sector + 0x40
    return

define refresh_anchor(player):
    let center = placement_anchor[player] - 0x40
    if scenario != 8 and free_neighbours(player, center) > 0 and sector_gang_count[player * 64 + center] <= 5:
        return
    placement_anchor[player] = choose_anchor(player, placement_anchor[player]) + 0x40
    return
```

## Outputs

`refresh_anchor` changes `placement_anchor[player]` when the anchor fails its
tests. The stored value is a sector plus `0x40`, or 63 when no replacement is
found. Makes no draw.

## Edge cases

When every pass fails, the anchor becomes 63, which decodes to -1. At the next
refresh `free_neighbours` reads the owner byte before the sector list for it
(BUG-AI-002), and a hire placed there goes to sector -1. In Big Man the anchor
is always replaced, and a failed scan keeps the old anchor, including 63 and
164 (sector 100), even when that sector is full. The neighbourhood scans also
read index 64, which lies past the last sector (BUG-AI-002).

## What the sources say

SRC-MANUAL-GOG does not describe where computer players place their hires.

## Differences between builds

None known.

## Open questions

- `g_004A08C4` stands for the byte 36 bytes before the sector list, the owner
  byte of the sector at index -1. What the original keeps there is not
  recorded.
- The order of the three keep tests (scenario, free neighbours, occupancy) and
  whether they stop at the first failure are not recorded; the occupancy test
  for centre -1 would read the element before the player's row of
  `sector_gang_count`.
- In Big Man the first list the scan tests is empty (a radius-zero list); it is
  left out of the procedure.
- Where `seed_anchor` runs within new-match set-up is not recorded; it needs
  the Right Hands' sector, so it runs after the headquarters are placed.
- The occupancy counts are taken from `sector_gang_count`; the findings speak
  of "occupancy below six" without naming the array.
- An anchor of 164 (sector 100) in Big Man is kept because the scan returns the
  incoming anchor minus 64; what `free_neighbours` would read for it is not
  reached, since Big Man skips the keep test.
