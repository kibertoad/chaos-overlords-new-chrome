---
id: FND-AI-069
title: The sector selector scores on the owner query, ends mode 6 after its hostile-human bonus, and multiplies table element x * 9 + y in its common block
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408AA4..0x00408B31
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040983D..0x00409854
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408B31..0x00409894
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408DF2..0x00409034
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409894..0x004098D4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004098D4..0x004099BE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004099F8..0x00409B6D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004045BB..0x0040465C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00404539..0x004045B7
tool: Ghidra 12.1.3
environment: null
---

## Observation

The sector selector `0x00408642` runs its radius `ebp-0xC` from 1 to 7
(`0x00408AB0`). For each radius it walks the column offset `ebp-0x14` from
`-radius` to `radius` and, inside it, the row offset `ebp-0x18` the same way,
skipping a column or a row off the board, so it visits the whole square at
every radius, column by column. The gang's column is at `ebp-0x20` and its row
at `ebp-0x28`; the visited sector is `(row) * 8 + column`, and every case body
adds to the score table dword at `0x0048A150 + column * 32 + row * 4`.

For each visited sector the mode, less 1, indexes the jump table at
`0x00409854` when it is at most 15 unsigned (`0x0040983D..0x00409854`). Any
other mode goes to `0x00409894`.

Every case that tests an owner reads it with selector `0x21`, the owner byte or
-2 for a sector under a Crackdown, never the sector record directly:

- mode 1 adds 1 when the owner is -1 and selector `0x2C` passes;
- mode 2 adds 1 when the owner is the player;
- mode 3 adds 1 when the owner is another player's slot (not the player, not
  below 0);
- mode 4 passes the owner to selector `0x2D`;
- mode 5 makes three separate tests on the owner: -1 with selector `0x2C`
  adds 5, the player with selector `0x5B` at 0 adds 2, another player's slot
  adds 1;
- modes 7, 8 and 9 require the player as owner;
- mode 10 with no human (selector `0x32` below 1) adds 1 for another player's
  slot; with a human it adds 1 when selector `0x35` passes for the sector,
  with no owner test of its own;
- modes 12 and 13 leave out a sector whose owner is the player; modes 12 to 15
  admit their listed sectors (27, 28, 35 and 36; 9, 12, 30, 33, 51 and 54) while
  `sector_gang_count` of the player is below 6 there, and add 5 when the
  attitude of the player toward the owner (`0x004AB590 + player * 0x18 +
  owner * 4`) is below 0 and selector `0x35` passes for the sector, else 1;
- mode 11 adds 1 to the sector of selector `0x5A` and mode 16 to the sector of
  selector `0x77`.

Mode 6 (`0x00408DF2..0x00409034`) first tests selector `0x32` above 0, the
attitude toward the owner below 0 and selector `0x35` for the sector. When all
three pass it adds 2, sets `ebp-0x24` and jumps to `0x00409894`
(`0x00408E80`), past the leader tests. Otherwise it reads selector `0x2E`:
when that is neither the player nor -1 it adds 1 to a sector whose owner is
that player; when it is -1 it adds 1 when the owner is 0 or more and its byte
in `scenario_standing` (`0x004ABC08`) is 0; when it is the player it adds 1
when the owner is 0 or more and not the player and selector `0x5E` gives fewer
than 4.

At `0x00409894` a mode of `0x40` or more adds 1 to the dword at
`0x0048A150 + (t / 8) * 4 + (t % 8) * 32`, `t = mode - 0x40`, whatever sector
is being visited, and sets `ebp-0x24`.

Every visit then reaches the common block at `0x004098D4`. It reads selector
`0x21` for the visited sector, compares the attitude entry at `0x004AB590 +
player * 0x18 + owner * 4` with 0 with no range test on the owner, and when it
is below 0 calls selector `0x35` for the visited sector. When that passes it
loads the dword at `0x0048A150 + column * 32 + (column + row) * 4`, multiplies
it by five with `LEA ECX,[ECX+ECX*4]` and stores it back at the same address
(`0x0040992F..0x004099B4`). The row term is computed as `(row * 8) % 8` for
the first index and `(row * 8) / 8` added to the column for the second.

After the square, `0x004099BE` leaves the radius loop when `ebp-0x24` is set.
The loop at `0x004099F8..0x00409B6D` then clears every sector whose byte at
sector record +15 is nonzero. It reads the acting planning record's family
byte: only families 0 and 1 call selector `0x2C`, and for them a sector the
player does not own (selector `0x21`) is cleared when the gang cannot take it
by Control on its own.

Selector `0x77`, at `0x004045BB..0x0040465C`, scans all 81 planning slots in
ascending order without testing whether the gang is active, counts only the
records whose family byte is 11, treats ordinals 0, 6, 12 and so on as block
leaders, and on reaching the acting slot returns the first auxiliary value of
the most recent leader. Selector `0x76`, at `0x00404539..0x004045B7`, returns
1 only when the acting slot is itself a leader.

## Interpretation

The score of sector `row * 8 + column` is table element `column * 8 + row`.
The common block multiplies element `column * 9 + row` instead. In column 0
that is the visited sector, which already holds its score for this visit. In
any other column it is the sector `column` rows further down the same column,
or, once `column + row` reaches 8, a sector of the next column; either way a
sector the ring visits later in the same square, whose score is still 0 unless
an encoded mode added to it. In column 7 with a row of 1 or more the element
lies past the 64 of the table, in the first seven dwords of player 0's
planning records (`0x0048A250`), which it multiplies by five.

So for the scores the modes add, the multiply by five changes only sectors of
column 0. An objective of modes 12 to 15 never lies in column 0, so a hostile
human's objective scores 5. An encoded target's count is multiplied when a
hostile human's sector whose element maps onto it is visited after the target
has been counted.

A sector under a Crackdown reads as owner -2: it scores nothing in the modes
that test an owner, so it neither scores nor stops the search, and for the
attitude test it reads the entry two before the player's row. A neutral sector
reads the entry one before. Selector `0x35` for a neutral sector reads the
controller table at index -1 (RULE-AI-004).

Mode 6 gives a hostile human's sector 2 and no leader point.

## Alternatives

The row and column names of the stack slots follow from the sector number the
cases pass to selector `0x21`. Read the other way round the index would be
`row * 9 + column`; EXP-TURN-010 does not tell the two apart at call 11610, but
the instructions do.

## How to reproduce

Disassemble `0x004098D4..0x004099BE` and compare its address arithmetic with
the `INC` at `0x00408DE2`; disassemble `0x00408DF2..0x00408E85` for the jump
out of mode 6; read the `0x21` calls of each case between `0x00408B31` and
`0x00409894`.
