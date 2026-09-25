---
id: RULE-HIRE-003
title: A human player holds at most one hire or snub order, set by dragging an offer or pressing Reject
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-HIRE-004, FND-HIRE-001, SRC-MANUAL-GOG]
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

## What the sources say

SRC-MANUAL-GOG, page 17, says a gang is hired by dragging its picture to a
sector the player controls or one where a gang of the player's already is, and
that the Reject button under an offer fires it. The manual does not say that
only one offer can be acted on per turn.

## Differences between builds

None known.

## Open questions

- Which sectors the drop accepts (the manual's owned-or-occupied test, and
  whether a sector already holding six of the player's gangs is refused) is
  not recorded from the handler.
- Whether the Reject branch clears the other two slots is stated for the
  handler as a whole and has not been confirmed for that branch.
- The order a computer player's hire planner writes is covered by the AI
  rules, not here.
