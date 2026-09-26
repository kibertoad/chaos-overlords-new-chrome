---
id: FND-CONTROL-003
title: Control settles only sectors with a Control order and no police, adds the defenders, Income and Support to the owner's pool, and subtracts Income and Support from every pool
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475284..0x00475856
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Control block of the whole-turn resolver `fn_00472775` (FND-EXE-004 gives
its range) runs at `0x00475284..0x00475856`, after the Move pass.

1. `0x00475284..0x004752EB` clears the local 6-by-64 array of 32-bit pools and
   a 64-byte array of sector marks.
2. The pool build, `0x004752FD..0x00475404`, scans player slots 0 to 5 and
   roster slots 0 to 80. A record whose sector byte is 100 is skipped
   (`0x00475374`). For a record whose action byte is 4 (`0x004753ED`) it adds
   the Force byte (offset `0x03`, `0x0047538F`) plus the Control byte (offset
   `0x17`, `0x00475396`) to the pool cell of the gang's sector in the row of
   the record's own player byte (offset `0x00`, `0x004753B3`). It then reads
   the sector record's byte at offset `0x0F` (`0x004753C4`, police presence)
   and marks the sector only when that byte is 0 (`0x004753DB`).
3. The sector loop, `0x00475409..0x0047585D`, visits sectors 0 to 63 and skips
   every unmarked sector (`0x00475433`). For a marked sector whose owner byte
   is not -1 (`0x00475453`) it scans the owner's 81 records (`0x0047545C`):
   each whose sector byte equals the sector (`0x004754A9`) and whose action is
   not 8 (`0x004754E0`) adds its Force (`0x0047550C`) plus Control
   (`0x00475537`) to the owner's pool cell for the sector (`0x0047555E`). It
   then adds the sector record's bytes at offset `0x04` (`0x00475573`) and
   offset `0x06` (`0x00475584`) to the owner's cell (`0x004755AB`).
4. The winner scan, `0x004755B2..0x004756B6`, starts with a best margin of 0,
   a count of 0, a tie flag of 0 and candidate entry 0 of -1. For each player
   slot 0 to 5 the margin is the player's pool cell minus the sum of the
   sector bytes at offsets `0x04` (`0x00475625`) and `0x06` (`0x00475636`),
   the same sum for every player (`0x00475640`). A margin equal to the best
   sets the tie flag and appends the player (`0x0047564E..0x00475673`); a
   greater margin clears the flag, the count and entry 0 to that player and
   becomes the best (`0x00475686..0x004756B6`).
5. With the tie flag set, `0x004756D9` calls `fn_0045D227` with the count plus
   one and takes the candidate at that position, counting from 1; without it
   the winner is entry 0.
6. Nothing changes when the winner is -1 (`0x004756FF`) or equals the current
   owner (`0x0047571D`). Otherwise, when the previous owner is not -1
   (`0x00475740`), the block adds 1 to the winner's entry of `0x004A27A8`
   (`0x00475753`) and changes the previous owner's entry for the winner in the
   6-by-6 table at `0x004AB590` (`0x0047577E`, clamped at -10 at
   `0x004757B7`). It then stores the winner in the owner byte (`0x004757D4`),
   stores 0 in the bytes at offsets `0x08`, `0x0A` and `0x0C`, the progress
   bytes of the three site slots (`0x004757E4`, `0x004757F5`, `0x00475806`),
   and records two reports through `fn_00477748`: type 2 to the winner with
   the sector and the previous owner (`0x00475827`), then type 3 to the
   previous owner with the sector and the winner (`0x00475848`). It sets the
   sector's changed flag at `0x00498B78` (`0x00475856`). No other byte of the
   sector record is written.

`fn_00477748` returns without recording anything when its player argument is
-1 (`0x0047774E`), so the type 3 report of a neutral sector's capture is
dropped.

Earlier in the resolver, `0x00472964..0x00472A36` subtracts the sector bytes
at offsets `0x04` and `0x06` from the same local array for every player other
than an owned sector's owner. The pool array is cleared again at `0x004731DE`
and at step 1, so that subtraction has no effect on Control.

## Interpretation

- Only sectors where at least one active gang was ordered to Control, and
  where the police byte is 0, are settled. A Crackdown sector, including one
  with a permanent Crackdown, cannot change owner through Control, and a
  sector nobody tried to take is never touched.
- The pool of a gang is Force plus Control. The Income subtracted is the byte
  at offset `0x04` (`income`), not `cash_yield` at `0x03`, and the Support is
  the byte at `0x06`.
- The defense is not subtracted from the challengers. It is added, with Income
  and Support, to the owner's own pool, so after the common subtraction the
  owner competes with a margin equal to its own Control pool in the sector
  plus the Force and Control of its gangs there that are not hiding. A
  challenger's margin is its pool minus Income minus Support. The owner keeps
  the sector when its margin is the largest; a challenger takes it only with a
  strictly larger margin than the owner's, or by the draw when they tie.
- An owner's gang that is itself ordered to Control its own sector counts
  twice: once in the pool build and once as a defender.
- A player with no Control order in the sector has margin `-(income +
  support)`, or its defense when it is the owner.
- A capture raises the winner's `overthrow_count` only when the sector had an
  owner, and resets only the progress of the three sites; the site
  definitions stay.

## Alternatives

- The update of `0x004AB590` is the attitude change of the computer players;
  its meaning is left to the AI entries.
- Whether an owned sector's Crackdown byte can be nonzero with the owner's
  gangs still ordered to Control is a matter of play that this reading does
  not settle.

## How to reproduce

In `fn_00472775`, find the player-then-roster loop that compares the action
with 4 at `0x004753ED`. Follow the police test at `0x004753C4`, the sector
loop at `0x00475409` with its mark test, the defenders' scan with its
comparison with 8, and the winner scan whose subtraction reads `0x004A08EC`
and `0x004A08EE`. The capture writes follow the comparisons at `0x004756FF`
and `0x0047571D`.
