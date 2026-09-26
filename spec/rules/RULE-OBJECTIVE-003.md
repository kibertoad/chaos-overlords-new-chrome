---
id: RULE-OBJECTIVE-003
title: At the end of resolution, a player without the Right Hands in Eliminate loses everything, and any player with no sector and no gang leaves the match
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TURN-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-STATE-002, FMT-STATE-004]
---

## Summary

A player who controls no sector and has no gang left is out of the match. In
Eliminate, a player whose Right Hands has died is out as well: every sector
the player held becomes neutral, losing its influence progress, and every one
of the player's gangs vanishes.

## When it runs

In `turn_end`, near the end of `resolution`, before the elimination reports
are recorded (RULE-EVENT-003) and before RULE-OBJECTIVE-001.

## Parameters

None.

## Inputs

`scenario`, `gangs`, each sector's `owner`.

## Procedure

```text
if scenario == 7:
    # Eliminate
    for each player in turn_order:
        if gangs[player * 81].sector == GANG_INACTIVE:
            for s in 0..64:
                if sectors[s].owner == player:
                    sectors[s].owner = SECTOR_NEUTRAL
                    for k in 0..3:
                        sectors[s].sites[k].progress = 0
            for slot in 0..81:
                gangs[player * 81 + slot].sector = GANG_INACTIVE

for each player in turn_order:
    let alive = false
    for s in 0..64:
        if sectors[s].owner == player:
            alive = true
    for slot in 0..81:
        if gangs[player * 81 + slot].sector != GANG_INACTIVE:
            alive = true
    if not alive:
        player_active[player] = 0
```

## Outputs

No return value. In Eliminate, makes sectors neutral, clears their site
progress and retires gangs of players who lost the Right Hands. Clears
`player_active` of every player left with nothing. Makes no draws.

## Edge cases

Retiring a gang writes only its `sector` byte. Its Force, definition and
equipment stay in the record, where nothing can reach them; a later hire into
the slot overwrites the whole record. Nothing is credited back for the
equipment. This is the only place `player_active` is cleared.

## What the sources say

SRC-MANUAL-GOG, page 14 (Eliminate), says a player who loses the Right Hands
is out of the game, that player's gangs vanish and the sectors that player
controlled become neutral, which agrees with the executable.

## Differences between builds

None known.

## Open questions

- How the helper decides that roster slot 0 "no longer contains the active
  Right Hands" (its `sector` byte at 100, as written here, or also its
  `definition`) is not recorded.
- Whether a player already inactive is skipped in either loop is not
  recorded; the writes are the same either way.
- `player_active` has no recorded address.
