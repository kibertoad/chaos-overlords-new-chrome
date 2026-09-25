---
id: FND-AI-040
title: The sector selector breaks ties with one draw and routes one step, x then y, under a six-gang limit
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408553
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040A1A7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00489950..0x00489F50
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00408553` sorts the 64 sector scores in descending order and keeps each
score's sector number. The selector `0x00408642` then chooses uniformly among
all the sectors tied for the greatest score: a unique greatest score makes no
draw, and a tie makes one call of the bounded wrapper (three raw draws). When
the chosen sector lies outside the gang's 3-by-3 neighbourhood, the routine
moves one step along x and then one step along y, keeping each part only when
the resulting sector holds at most five of the active player's gangs; it can
therefore return a diagonal neighbour. When the chosen sector is adjacent and
its score is positive, it is returned directly.

Candidates are removed late when marked unavailable or when a family 0 or 1
gang cannot take a non-owned destination alone (FND-AI-027). When those
filters leave a greatest score below 1, the routine still counts the sectors
tied at the top, which are then all 64 sectors at score 0, makes one draw, and
routes toward the drawn sector the same way. It does not resume the radius
search and returns no special value.

`0x0040A1A7` clears 64 integers at `0x00489950 + player * 0x100`, scans the
player's 81 gang records, and adds one to the integer of each active gang's
sector. The routing's four comparisons with 5 read this array.

## Interpretation

A gang moves one sector per turn toward a chosen goal, and never into a sector
that already holds six of its player's gangs. Randomness enters only through
the tie draw (and the neighbour draw of mode 0).

## Alternatives

The sort's handling of equal scores does not matter, since the tie draw
counts all equal scores; the order of the tied sectors in the sorted list does
matter for which one the draw picks, and it is not recorded. The rule for
"outside the neighbourhood" with the chosen sector adjacent but scoring 0 is
not stated. When both steps are blocked the routine is said to return the
gang's own sector, which makes a Move to the sector the gang is in; the
instructions for that case are not recorded.

## How to reproduce

`0x00408553` is called from `0x00408642` after scoring; the wrapper call and
the four comparisons with 5 against `0x00489950 + player * 0x100` follow. The
clearing loop of 64 integers is in `0x0040A1A7`.

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
