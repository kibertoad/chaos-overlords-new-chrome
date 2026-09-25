---
id: RULE-MOVE-002
title: Move destinations are rewritten until no sector would hold more than six of the player's gangs
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-MOVE-001, FND-AI-005, FND-AI-028]
conflicting: []
split_with: []
related: [RULE-AI-006, RULE-AI-007, FMT-STATE-001]
---

## Summary

When a player's Moves would put more than six of their gangs in one sector,
the game sends movers back to where they started, earliest roster slot first,
until every sector fits. Crowded sectors are fixed from the highest sector
number down.

## When it runs

At the start of each player's part of `move_phase`, called by RULE-MOVE-001.

## Parameters

- `player`: the player slot whose destinations are normalized.

## Inputs

`gangs` of `player`, each gang's `sector`, `action` and `target`, and the state
of `rng` through `select_sector` (RULE-AI-006).

## Procedure

```text
while true:
    let counts: INT32[] = []
    for s in 0..64:
        append(counts, 0)
    let movers: INT32[] = []
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector == GANG_INACTIVE:
            continue
        if gang.action == ACTION_MOVE:
            counts[gang.target] = counts[gang.target] + 1
            append(movers, slot)
        else:
            counts[gang.sector] = counts[gang.sector] + 1
    let crowded = -1
    for s in 0..64:
        if counts[s] > 6:
            crowded = s
    if crowded == -1:
        return
    let chosen = -1
    for each slot in movers:
        let mover = gangs[player * 81 + slot]
        if mover.target == crowded and counts[mover.sector] < 6:
            chosen = slot
            break
    if chosen == -1:
        for each slot in movers:
            if gangs[player * 81 + slot].target == crowded:
                chosen = slot
                break
    let gang = gangs[player * 81 + chosen]
    if gang.target == gang.sector:
        # already sent back: the shared sector selector in mode 0 picks the destination
        gang.target = select_sector(player, 0, player * 81 + chosen)
    else:
        gang.target = gang.sector
```

## Outputs

No return value. Changes `target` of the movers it sends back. The only random
draws are those `select_sector` makes in mode 0 (RULE-AI-007), once for each
fallback.

## Edge cases

- Counts are projected: a gang that is not moving counts in its own sector and
  a mover counts in its destination, so two players' gangs never count
  together.
- When two otherwise legal Moves compete for the last place in a sector, the
  earlier roster slot is sent back and the later one moves.
- A mover sent back to its source still counts as a mover, now into its own
  sector. If that sector is itself crowded and no other mover into it can go
  back, the mode-0 selector is used.
- After every rewrite the counts are rebuilt from the start, so fixing one
  sector can create or clear another.

## What the sources say

SRC-MANUAL-GOG does not describe how competing Moves into a full sector are
settled. The six-gang limit is the manual's structural limit on friendly gangs
in a sector.

## Differences between builds

None known.

## Open questions

- What mode 0 of the sector selector does is disputed: FND-MOVE-001 describes
  one bounded draw over all 64 sectors followed by the selector's one-step
  routing, and FND-AI-028 a random pick among the eight neighbours
  (RULE-AI-007). That the fallback stores the selector's result as the new
  destination is not recorded either.
- Whether the loop can fail to finish, for example when the selector returns a
  sector that is still crowded, is not known.
- Which field holds a Move's destination is not recorded; the procedure uses
  `target`.
- Whether a mover's source is compared with `counts < 6` before or after its
  own projected count is removed is taken from FND-MOVE-001's words ("whose
  source's projected total is below six").
