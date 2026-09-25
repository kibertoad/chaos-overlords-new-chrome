---
id: FND-AI-037
title: The family-10 handler improves armor, equips item 44, heals, seeks Stealth sites, then raises Chaos or hides
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
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 10's handler `0x0042A6E0` first calls selector `0x72`, which scans the
researched armor items (type 3) whose Tech is at most the gang's own Tech and
keeps the first strictly greatest Defense improvement. An unarmored gang uses
item 1 as its zero-Defense baseline. The handler accepts the armor only when
the +14 cooldown is at most 0 and the item's cost is at most cash, and then
writes Equip and the literal cooldown 2.

If that fails, a gang with an empty miscellaneous slot writes Equip for item
44 when the player's research value for item 44 is clear. This branch makes no
separate Tech or cash comparison. Otherwise it writes Heal only when Force is
below 10, effective Heal is at least -3, and the cached opponent weight of the
current sector is exactly 0.

Otherwise it calls sector selector mode 9 and compares selector 8 for the
returned sector with selector 8 for the current sector. Selector 8 sums the
strictly positive Stealth of the sector's finished sites. When the returned
sector's sum is strictly greater, the handler writes Move and calls mode 9 a
second time for the destination; it does not reuse the first result, so with
a tie for the best score two draws are made and the second can pick a different
tied sector. When it is not greater, selector `0x5B` counts the player's gangs
in the sector with previous action Chaos: 0 writes Chaos, a positive count
writes Hide.

## Interpretation

Family 10 is a Siege defender and raider that prefers sectors whose sites
hide it well, and raises Chaos there unless another gang already does.

## Alternatives

Which item item 44 is, and why the handler singles it out, are content and are
not described. Whether "research value clear" means 0 in the research table is
assumed.

## How to reproduce

Open `0x0042A6E0`; the literal 2 stored as cooldown, the comparison with item
number 44 (`0x2C`), and the two mode 9 calls to `0x00408642` (FND-AI-028).

This evidence was recorded in the older notes under the endgame awards
heading; it concerns only the AI.
