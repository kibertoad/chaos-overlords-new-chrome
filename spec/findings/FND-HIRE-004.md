---
id: FND-HIRE-004
title: The human hire handler keeps at most one hire or snub order per player and toggles Reject
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00416C75
tool: Ghidra 12.1.3
environment: null
---

## Observation

The human hire handler `0x00416C75` writes the order byte of the selected
offer slot (`0x004A27C8 + player * 3 + slot`) and writes -1 into the order
bytes of the other two slots.

- Dragging an offer onto a sector writes that sector into the slot's order
  and sets the other two orders to -1. A later drag of any offer does the same,
  so it replaces the earlier choice, and dragging the same offer again changes
  its sector.
- Its Reject branch changes the selected slot's order from -1 to -2 or from -2
  to -1. When the order holds a sector, Reject sets it to -1.
- The handler neither reads nor writes cash or cash spent.

## Interpretation

A player gives at most one hire-related order per turn: either one hire into
one sector or one snub. Reject on a slot already ordered to hire cancels the
hire; a second Reject is needed to snub it. Cash is not checked when the
order is given.

## Alternatives

This finding was recorded as part of FND-HIRE-001 before it was split. Whether
the Reject branch also clears the other two slots' orders is stated for the
handler as a whole and has not been confirmed for that branch alone. What
checks the drop target (a sector the player owns, or one holding a friendly
gang, as the manual says) is not recorded.

## How to reproduce

`0x00416C75` writes the order array at `0x004A27C8`; find its writes of -1,
-2 and a sector value, and the loop that clears the two other slots.
