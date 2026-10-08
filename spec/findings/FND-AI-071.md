---
id: FND-AI-071
title: The family-10 handler improves armor, equips item 44, heals, moves when the mode-9 sector's last finished site hides better, then raises Chaos or hides
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042A6E0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042A9AE..0x0042AA2E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 10's handler `0x0042A6E0` first calls selector `0x72`, which scans the
researched armor items (type 3) whose Tech is at most the gang's own Tech and
keeps the first strictly greatest Stealth improvement (FND-AI-055). An
unarmored gang starts from item 1. The handler accepts the armor only when
the +14 cooldown is at most 0 and the item's cost is at most cash, and then
writes Equip and the literal cooldown 2.

If that fails, a gang with an empty miscellaneous slot writes Equip for item
44 when the player's research value for item 44 is clear. This branch makes no
separate Tech or cash comparison. Otherwise it writes Heal only when Force is
below 10, effective Heal is at least -3, and the cached opponent weight of the
current sector is exactly 0.

Otherwise it calls sector selector mode 9 for the acting slot
(`0x0042A9BC`), passes the returned sector to selector 8 (`0x0042A9C7`), keeps
the result in `EBX`, calls selector 8 for the gang's current sector
(`0x0042A9DB`) and compares the two with `JLE` (`0x0042A9E5`). When the
returned sector's value is strictly greater, the handler writes Move and calls
mode 9 a second time for the destination (`0x0042AA29`); it does not reuse the
first result, so with a tie for the best score two draws are made and the
second can pick a different tied sector. When it is not greater, selector
`0x5B` counts the player's gangs in the sector with previous action Chaos: 0
writes Chaos, a positive count writes Hide.

Selector 8 in the query function `0x00402D70` starts from 0 and walks site
slots 0, 1 and 2 of the sector. For each slot where selector `0x1C` is nonzero
it replaces its value with the Stealth of that slot's site definition (the
word at `0x004AB68C + definition * 0x3E`). Selector `0x1C` is 1 when the
definition's Resistance minus the slot's progress is below 1, that is when the
site is finished.

## Interpretation

Selector 8 gives the Stealth of the last finished site in the sector, whatever
its sign, and 0 when none is finished. It does not add the sites up and does
not leave out a negative Stealth. A sector whose third finished site has
Stealth 0 therefore scores 0 even when an earlier finished site has Stealth 1.

EXP-TURN-013's first run shows it: in turn 22 player 2's first gang, in
sector 54 (sites finished in all three slots, the last with Stealth 0), moves
to sector 61 (only slot 0 finished, Stealth 1), and player 5's, in sector 26
(slots 0 and 1 finished, the second with Stealth 0), moves to sector 25 (only
slot 0 finished, Stealth 1). A sum of the positive Stealth scores 1 for all
four sectors and leaves both gangs where they are.

Sector selector mode 9 scores a sector with its own sum of the positive
Stealth of the finished sites (RULE-AI-006); selector 8 is used only for the
comparison.

## Alternatives

Which item item 44 is, and why the handler singles it out, are content and are
not described. Whether "research value clear" means 0 in the research table is
assumed.

## How to reproduce

Open `0x0042A6E0`; the literal 2 stored as cooldown, the comparison with item
number 44 (`0x2C`), and the two mode 9 calls to `0x00408642`. In `0x00402D70`,
read case 8: the loop over three slots calling the function itself with
selector `0x1C`, and the plain store of the Stealth word in the loop body.
