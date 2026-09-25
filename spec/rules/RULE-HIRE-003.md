---
id: RULE-HIRE-003
title: A human player holds at most one hire or snub order, set by dragging an offer or pressing Reject
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HIRE-004, FND-HIRE-001, FND-HIRE-008, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

Dragging an offer onto a sector orders that gang hired into that sector.
Pressing Reject under an offer marks it to be fired, and pressing it again
unmarks it. Each new choice replaces the previous one, so a player hires or
fires at most one offer per turn. Nothing is paid until the turn resolves.

## When it runs

During a human player's part of `planning_phase`, each time the player drops
an offer on a sector or presses an offer's Reject control.

## Parameters

- `player`: the acting player slot.
- `slot`: the offer slot, 0 to 2, the input acted on.
- `destination`: the sector the offer was dropped on, or -2 when Reject was
  pressed.

## Inputs

`hire_orders` for the player's three slots.

## Procedure

```text
let i = player * 3 + slot
let value = destination
if destination == -2:
    if hire_orders[i] == -1:
        value = -2
    else:
        value = -1
for other in 0..3:
    if other != slot:
        hire_orders[player * 3 + other] = -1
hire_orders[i] = value
```

## Outputs

No return value. Sets the chosen slot's order to the sector, to -2 (snub) or
to -1 (none), and sets the other two slots' orders to -1. Changes neither
`cash` nor `cash_spent`, and makes no random draw.

## Edge cases

- Reject on a slot ordered to hire cancels the hire; pressing Reject again
  then snubs the offer.
- Dragging a snubbed offer onto a sector turns the snub into a hire.
- Dragging an offer already ordered to hire onto another sector moves the
  order to that sector.
- Cash is not tested, so a player can order a hire it cannot pay for; the
  hire then fails at resolution (RULE-HIRE-001).
- A drop is accepted on a sector the player owns or one where the player has
  a living gang, read from the player's `gangs_seen` byte (FMT-STATE-002);
  anywhere else the order is left unchanged (FND-HIRE-008). The drop does not count the gangs
  already there, so a sector holding six of the player's gangs is accepted
  and the hire fails at resolution.
- The Reject branch clears the other two slots' orders like a drop does
  (FND-HIRE-008).

## What the sources say

SRC-MANUAL-GOG, page 17, says a gang is hired by dragging its picture to a
sector the player controls or one where a gang of the player's already is, and
that the Reject button under an offer fires it. The manual does not say that
only one offer can be acted on per turn.

## Differences between builds

None known.

## Open questions

- The order a computer player's hire planner writes is covered by the AI
  rules, not here.
