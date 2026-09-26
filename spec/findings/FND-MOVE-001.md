---
id: FND-MOVE-001
title: The Terminate pass runs before the Move pass, and Move destinations are normalized per player to at most six gangs a sector
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476A94
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
tool: Ghidra 12.1.3
environment: null
---

## Observation

The whole-turn resolver `fn_00472775` has two separate passes after the Chaos
payout, each a scan over player slots 0 to 5 and, inside each, the player's
81 gang records in ascending order.

1. The first pass handles action 14 (Terminate). For each record it copies the
   whole 32-byte record out, tests for action 14, sets only the copy's sector
   byte to 100 and copies all eight 32-bit words back.
2. Only after the first pass has finished for every player does the second
   pass handle action 10 (Move). At the start of each player's part of this
   pass it calls `fn_00476A94`. It then copies each remaining action-10
   record's stored destination sector into the record's sector, with no
   further capacity test.

`fn_00476A94` repeats these steps until no sector is over six:

- It rebuilds two 64-entry count arrays for the player. Every active record
  whose action is not 10 adds one to the count of its current sector. Every
  active action-10 record adds one to the count of its destination and is
  appended to a list of movers in ascending roster slot order.
- Its scan of the sector counts keeps the last total above six, so the
  highest-numbered sector with more than six gangs is repaired next.
- It looks for the earliest mover into that sector whose source sector's
  projected total is below six and rewrites that mover's destination to its
  source sector. If there is no such mover, it rewrites the earliest mover into
  that sector anyway.
- When the record it picks has already been rewritten to its own source (its
  destination equals its sector), it instead calls the sector selector
  `fn_00408642` with the literal mode 0. In mode 0 no sector gets a positive
  weight, so the selector makes one bounded random draw over all 64 sectors,
  which are all tied, and then applies its usual one-step routing toward the
  chosen sector, horizontal first and then vertical, with a capacity check.
- After every rewrite it starts again from the recount.

## Interpretation

Every Terminate is carried out before any Move, whatever order the orders
were given in. In each pass, gangs are handled by player slot and then roster
slot. Moves that would put more than six of a player's gangs in one sector are
not settled by who moved first: the earliest roster slot moving into the
crowded sector is sent back to its source, where it stays as a Move that goes
nowhere, and later roster slots move. Several crowded sectors are repaired
from the highest sector number down, with a full recount after each rewrite.
Terminate changes nothing in the gang record but the sector byte, so the
equipment and other fields stay as they were.

## Alternatives

- Which field holds the Move destination is not recorded; the observation
  calls it the stored destination sector.
- Whether the rewrite in mode 0 stores the selector's result as the new
  destination, and what the selector returns when capacity blocks every step,
  has not been written down here. FND-AI-005 describes the selector, and the
  AI rules read its mode 0 as a random pick among the eight neighbouring
  sectors instead of a draw over all 64; the two readings have not been
  reconciled against the selector's instructions.
- Whether "active" in the count means a sector byte other than 100 is assumed
  from the rest of the resolver, not recorded for this helper.

## How to reproduce

In `fn_00472775`, after the pass that credits the Chaos payout, find two
player-then-roster double loops: the first tests action 14 and stores 100 in
the sector byte of the copied record; the second tests action 10 and begins
each player with a call to `fn_00476A94`. In `fn_00476A94`, the two 64-entry
arrays, the comparison with 6 and the call to `fn_00408642` with a first
argument of 0 lead to the steps above.
