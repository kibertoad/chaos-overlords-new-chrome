---
id: FND-AI-066
title: The sector selector keeps its score pairs between calls, skips filtered sectors when refilling them, and counts ties past the end of the list
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004099E9..0x00409B6C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408553..0x00408642
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409B71..0x00409DCA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040A364
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048992C..0x00489950
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

The sector selector `0x00408642` keeps its scores in a table of 64 INT32 at
`0x0048A150`, which it clears at `0x00408A1C` on entry. The score of sector
`c` is at `0x0048A150 + (c % 8) * 32 + (c / 8) * 4`, so the table runs to
`0x0048A24F` and its dword `x * 8 + y` belongs to sector `y * 8 + x`.

The loop at `0x004099E9` then runs `c` from 0 to 63:

1. When the byte at `0x004A08F7 + c * 0x24` (sector record +15) is not 0, it
   stores 0 in the table entry of `c`.
2. It reads the family byte of the acting planning record at
   `0x0048A250 + player * 0x510 + idx * 16`. When the family is 0 or 1, it
   calls selector `0x2C` for `c` and, when that returns 0, selector `0x21`
   (the owner of `c`). When either passes it copies the table entry of `c`
   into the score field of pair `c` at `0x00489F50 + c * 8`
   (`0x00409AF5`). When neither passes it stores 0 in the table entry and
   leaves pair `c` as it is (`0x00409B26`).
3. Any other family copies the table entry into pair `c` (`0x00409B65`).

The pairs at `0x00489F50` are 64 records of an INT32 score at +0 and an INT32
sector at +4. Only `0x00408553` and `0x00408642` address them, and nothing
clears them, so a pair keeps its score from one call to the next.

`0x00408553`, called at `0x00409B71`, first stores `i` in the sector field of
pair `i` for `i` from 0 to 63. Then for `i` from 0 to 63 and `j` from `i` to
63 it compares the two scores and, when the score of pair `i` is below the
score of pair `j` (`JGE` at `0x004085CC` skips otherwise), swaps both fields
of the two pairs. The pairs end sorted by score, highest first, and the order
of equal scores is whatever the swaps leave.

After the sort, `0x00409B76..0x00409BF1` tests the sector field of pair 0
against the gang's column (`ebp-0x20`) and row (`ebp-0x28`), with signed
`%` and `/` by 8, for a column and a row each within one, and the score of
pair 0 for being above 0. When all pass, the loop at `0x00409BF1` counts `n`
from 1 while the score of pair `n` equals the score of pair 0; it has no other
bound. When `n` is above 1 it calls `roll(n)` at `0x00409C24` and reads the
dword at `0x00489F4C + r * 8`, the sector field of pair `r - 1`, and when `n`
is 1 it reads the sector of pair 0. The selector returns that value.

When the test fails, the code at `0x00409C48` sets the result to 0, adds
selector `0x5A` (the gang's sector), counts ties the same way and calls
`roll(n)` at `0x00409C99`. With the target read as before and the gang's
sector `s`, it compares `s % 8` with the target `% 8` and `s / 8` with the
target `/ 8`, all signed. For a smaller column it adds 1 to the result, reads
`sector_gang_count` at `0x00489950 + player * 0x100 + result * 4`, and
subtracts the 1 again when the count is above 5; a larger column subtracts 1
the same way, and the rows add or subtract 8. The four tests use the gang's
own column and row, not the moved result.

`sector_gang_count` at `0x00489950` is written by `0x0040A1A7` only: it
stores 0 in the player's 64 entries (`0x0040A364`) and adds 1 for each gang
(`0x0040A8E3`), for the player it is called with, so each row holds the counts
of that player's last refresh (FND-AI-045 gives the calls). The 36 bytes before
it, `0x0048992C..0x00489950`, lie in the initialized part of `.data` and hold
the INT32 values 272, 303, 333, 364, 0, 0, 0, 0 and 0 in the file; no
instruction addresses them. Its last row ends at `0x00489F4F`, where the pair
list begins.

The pair list ends at `0x0048A14F`. The table follows it at `0x0048A150`, and
the planning records (FMT-STATE-007) at `0x0048A250`, `0x510` bytes per
player, follow the table.

## Interpretation

A tie count that reaches pair 64 reads the table as pairs: an even dword of the
table as a score and the odd dword after it as a sector. From pair 96 on it
reads the planning records the same way, eight bytes per pair. It stops at the
first dword that differs from the top score. When the top score is 0, all 64
pairs hold 0, and the table is all 0, the count takes in 96 pairs and then the
records of player 0 for as long as they are zero bytes. A player's records are
all zero bytes until its first planning pass resets them to family 99
(RULE-AI-001), so with a human in slot 0 the count runs over that player's
whole block of 162 pairs, 64 + 32 + 162 = 258 in all, and stops at the first
record of player 1 unless its first four bytes are all 0. A pick past the 64 pairs returns a sector field
from the table or the records.

Because the filter of families 0 and 1 does not refill the pairs it clears, a
sector that another call scored keeps that score in its pair. After the sort it
can head the list, make the adjacency test fail, and join the tie count, as a
sector the current call never scored.

The table is the 8-by-8 score map of this selector. FND-STATE-007 gives the
region `0x0048A150..0x0048A210` as "eight counters per player"; the stride of 4
per row and 32 per column above shows it is this table and that it ends at
`0x0048A24F`, where the planning records begin.

## Alternatives

The sector fields of pairs 0 to 63 are reset before each sort, so pair 0's
sector is always 0 to 63 and the adjacency test reads a real sector. Nothing
bounds the tie count, so a count that runs through every record of all six
players would continue into `aux_records` at `0x0048C0B0`; no run has been
seen to go that far.

## How to reproduce

Read the 36 bytes of the file's `.data` section at `0x0048992C`.

Disassemble `0x004099E9..0x00409B6C` for the refill, `0x00408553..0x00408642`
for the sort, and `0x00409B71..0x00409DCA` for the two tie counts and the
routing. Search the file for the little-endian addresses `0x00489F50`,
`0x00489F54` and `0x00489F4C`: every reference falls inside those ranges.
