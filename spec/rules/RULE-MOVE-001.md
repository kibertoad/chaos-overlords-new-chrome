---
id: RULE-MOVE-001
title: Move pass carries out every Move, player by player, after normalizing each player's destinations
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-MOVE-001, FND-MOVE-002, FND-MOVE-003, FND-CONTROL-002, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-MOVE-002, RULE-TERMINATE-001, FMT-STATE-001]
---

## Summary

Gangs ordered to Move step into the sector chosen for them, one player after
another. Before a player's gangs move, the game sends back any mover that would
leave more than six of that player's gangs in one sector.

## When it runs

`move_phase`: after `terminate_phase` (RULE-TERMINATE-001), so every Terminate
has already been carried out, and before `control_phase`.

## Parameters

None.

## Inputs

`turn_order`, `gangs`, and each gang's `sector`, `action` and `target` (the
destination sector of a Move).

## Procedure

```text
for each player in turn_order:
    call RULE-MOVE-002(player)
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_MOVE:
            gang.sector = gang.target
```

## Outputs

No return value. Sets `sector` of each moving gang to its destination, in
player slot and then roster slot order. A mover that RULE-MOVE-002 sent back
has a destination equal to its sector and does not change. No capacity test is
made here, and no random draw except those RULE-MOVE-002 makes.

## Edge cases

- Moving a player's last gang out of a sector does not give up ownership of it
  (FND-CONTROL-002).
- The Move panel offers only the eight sectors around the gang's own
  (FND-MOVE-002), but this pass copies whatever destination is stored, so a
  destination set another way, such as by RULE-MOVE-002's fallback or a
  computer player's order, is carried out as it is.
- A gang whose record is inactive (sector 100), such as one that died in this
  turn's combat, keeps its order but does not move [FND-MOVE-003].
- The destination is the `target` byte, written by the Move panel
  [FND-MOVE-003].
- A Move records no Last Turn report [FND-MOVE-003].

## What the sources say

SRC-MANUAL-GOG, page 32 (Move), says Move takes a gang to an adjacent sector
chosen on the panel. Page 45 (Command Sequence) puts Move and Terminate in the
Movement phase, after Chaos and before Control. The manual does not describe
the six-gang limit's handling of competing moves or the order of Terminate
before Move.

## Differences between builds

None known.

## Open questions

None known.
