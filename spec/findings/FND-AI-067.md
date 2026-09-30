---
id: FND-AI-067
title: The sector selector adds 1 to an encoded mode's sector for every sector a ring visits
status: superseded
builds: [BLD-GOG-EN-1.1]
superseded_by: [FND-AI-069]
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408AA4..0x00408B31
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040983D..0x0040984D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409894..0x004098D3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004098D4..0x004099BE
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

The sector selector `0x00408642` runs its radius `ebp-0xC` from 1 to 7
(`0x00408AB0`). For each radius it walks the column offset `ebp-0x14` from
`-radius` to `radius` and, inside it, the row offset `ebp-0x18` the same way,
skipping a column or a row off the board. It visits the whole square at every
radius, column by column.

For each visited sector it copies the mode into `ebp-0x34` (`0x00408B2B`),
subtracts 1 and, when the result is at most 15 unsigned, jumps through the
table at `0x00409854` (`0x0040983D..0x0040984D`). Any other mode, 0x40 and up
among them, goes to `0x00409894`.

At `0x00409894`, when the mode is at least 0x40, the code computes
`t = mode - 0x40` and adds 1 to the dword at
`0x0048A150 + (t / 8) * 4 + (t % 8) * 32` (`0x004098C9`), then stores 1 in
`ebp-0x24` (`0x004098D0`). The added dword does not depend on the sector
being visited.

At `0x004098D4` every path reaches the common block: when the attitude of the
player toward the owner of the visited sector (`0x004AB590`) is below 0 and
selector `0x35` for that sector returns true, the score of the visited sector
is multiplied by five. The case bodies add to the score table (`INC`, `ADD`)
and never store into it.

After the square, `0x004099BE` leaves the radius loop when `ebp-0x24` is set.

## Interpretation

`ebp-0x24` is the `found` flag of RULE-AI-006. An encoded mode sets it on the
first sector visited, so its search always stops after radius 1, and its
target's score is the number of sectors of the board within one step of the
gang, its own included: 4, 6 or 9. When the target is one of those sectors and
is held by a hostile human, the multiply by five applies to the count reached
at its visit, and later visits add 1 each.

By FND-AI-066 the dword `(t / 8) * 4 + (t % 8) * 32` bytes into the table is
the score of sector `t` for `t` from 0 to 63. The end marker 100 of the
family-6 guard list (FND-AI-068) gives element 44, the score of sector 37.

Because the case bodies only add and the search stops after the first radius
that set `found`, every sector of the inner radii held 0 when the last radius
began, so adding and assigning give the same scores.

EXP-TURN-007 depends on it. In turn 10 of its first run a gang of player 1 in
sector 16 made an encoded call for sector 2, which sorted a score of 6 into
pair 0. The next call, for a family-0 gang of player 2 in sector 42, filtered
sector 0 and left that pair as it was (FND-AI-066), so the pair headed the list
alone and the gang stepped toward sector 0, to 33, with no draw. A score of 1
would have tied with the sectors that call scored and drawn `roll(2)`.

## Alternatives

The table dispatch covers modes 1 to 16. Modes 17 to 63 reach `0x00409894`,
fail the compare and score nothing; no caller passes them.

## How to reproduce

Disassemble `0x00408AA4..0x00408B31` for the two offset loops,
`0x0040983D..0x0040984D` for the dispatch, `0x00409894..0x004098D3` for the
encoded case and `0x004098D4..0x004099BE` for the common block and the exit
test.
