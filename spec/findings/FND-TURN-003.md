---
id: FND-TURN-003
title: The end of resolution clears eliminated players, reports each elimination to every player, and only then evaluates the objective
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476F3B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476857
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047712A
tool: Ghidra 12.1.3
environment: null
---

## Observation

The only code that clears a player's active byte is `fn_00476F3B`. It is called
once, near the end of the whole-turn resolver `fn_00472775`.

When the scenario is 7 (Eliminate), `fn_00476F3B` first scans the players in
slot order. For a player whose roster slot 0 no longer holds the active Right
Hands record, it scans all 64 sectors and, in each sector that player owns,
writes owner -1 and sets the progress of all three sites to 0. It then scans
all 81 of the player's gang records and writes 100, the inactive sector value,
into each one's sector byte. It writes nothing else in those records: Force,
definition, equipment and the other bytes keep their values.

For every scenario, `fn_00476F3B` then applies one test to each player: the
player stays active if it owns a sector or has a gang whose sector byte is not
100, and otherwise its active byte is cleared.

In `fn_00472775`, the code around the call compares each player's active byte
before and after it. For each player that was active and is no longer, it adds
a Last Turn report of type 9 for each of the six recipients: the eliminated
players in slot order, and for each of them the recipients in slot order.
After those reports it calls the end evaluator `fn_00476857`. The evaluator
rebuilds the scenario standings, tests whether one active player is left, and
then tests the scenario's own end condition. In Big Man, the scorer
`fn_0047712A` adds the current owners' points for the four centre sectors to
the score kept from earlier turns instead of starting from 0, and the evaluator
ends the match when a score reaches 40 or more.

## Interpretation

Elimination is decided after everything else in the turn and before the
objective. In Eliminate, losing the Right Hands takes away all of a player's
sectors, with their site progress, and every gang, and the ordinary test then
finds the player eliminated in the same pass. The retired gang records keep
their equipment bytes; the items are not returned to anyone, and the next hire
into the slot overwrites them. A combat death leaves equipment in the record the
same way.

## Alternatives

Which test decides that roster slot 0 "no longer holds the active Right Hands
record" (its sector byte being 100, its definition, or both) has not been
recorded. Whether the Eliminate scan skips players that are already inactive
has not been recorded. The instruction addresses of the call to `fn_00476F3B`,
of the report loops and of the call to `fn_00476857` have not been recorded.

## How to reproduce

List the writes to the player active bytes: the only function that clears them
is `fn_00476F3B`, and its only caller is `fn_00472775`. In `fn_00476F3B`, the
test of the scenario value against 7 guards the sector and gang scans. After
the call in `fn_00472775`, the nested loops over 6 players and 6 recipients
store report type 9, and the next call goes to `fn_00476857`, which calls
`fn_0047712A` for Big Man.
