---
id: FND-MOVE-007
title: The Move panel's cell table disables only neighbours beyond the city's edge or with owner byte -2, and its keys draw the pressed faces
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00411119..0x00411D9A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004413EF..0x004425AD
tool: Ghidra 12.1.3
environment: null
---

## Observation

The nine bytes at `0x004ABC40` are written only by `fn_00411119(sector)`, the
nine-sector display of the sector view (FND-UI-018): the references to
`0x004ABC40` are its stores and the reads in the Move handler `fn_004413EF`
and the Hire handler `fn_00416C75` (`0x00417A61`). The function:

1. sets all nine bytes to 1 (`0x004111A9`);
2. when `sector < 8`, blacks out the top row of the display and clears bytes
   0, 1 and 2 (`0x004112F6`); when `sector > 55`, the bottom row and bytes 6,
   7 and 8; when `sector % 8` is 0, the left column and bytes 0, 3 and 6
   (`0x00411456`); when it is 7, the right column and bytes 2, 5 and 8;
3. clears byte `k` when the owner byte, offset 0 of the 36-byte record at
   `0x004A08E8`, of the sector at offset -9, -8, -7, -1, +1, +7, +8 or +9 is
   -2, for the eight bytes other than 4.

It makes no other test: not the number of the player's gangs in a neighbour,
not the neighbour's owner otherwise, not police presence.

`fn_004413EF` reads byte `c + 3 * r` of the table for a pressed cell and
ignores the cell when it is 0 or when it is the centre (FND-MOVE-004); writing
the order stores the destination with no further test. Its key handling
(event type 2) draws the pressed faces with `fn_00418CCC` (FND-UI-019):

- Enter (`0x0D`) or Execute (`0x2B`) with a destination chosen calls
  `fn_00418CCC(0, ...)` on the confirm face `(137,293)-(187,316)`
  (`0x00441D6D`) and writes the order; with none it plays slot 4.
- Escape (`0x1B`) calls `fn_00418CCC(1, ...)` on the Cancel face
  `(137,261)-(187,284)` (`0x00441E02`) and ends without an order.

## Interpretation

The Move panel enables every neighbour on the map. No instruction stores -2
in an owner byte (FND-UI-015), so the owner test disables a cell only for a
sector record that arrives with -2 from a block read. A neighbour that already
holds six of the player's gangs is enabled, and a Move there is accepted when
it is ordered and settled by RULE-MOVE-002. The panel's keys show the pressed
face for one tick of the presentation clock before they act, as the other
command panels do.

## Alternatives

- Which records reach the owner byte with -2 through a block read, if any, is
  not recorded.

## How to reproduce

In `0x00411119`, find the loop storing 1 at `0x004ABC40 + k` for `k` from 0
to 8, the four edge tests with their stores of 0, and the eight compares of
the byte at `0x004A08E8 + 36 * (sector + d)` with -2. List the references to
`0x004ABC40`. In `0x004413EF`, find the key test of `0x2B` and `0x0D` with the
call to `0x00418CCC` with 0 and the rectangle `(0x125,0x89,0x13C,0xBB)`, and
the test of `0x1B` with the call with 1 and `(0x105,0x89,0x11C,0xBB)`, both in
`(top,left,bottom,right)` order.
