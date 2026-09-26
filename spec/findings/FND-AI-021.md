---
id: FND-AI-021
title: AI equipment choice picks a weapon, then armor, gated by nearby danger and a replacement cooldown
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00434080
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

Before its post-equipment continuation (FND-AI-020), the family-1 handler
tries an Equip.

- Selectors `0x39` and `0x3A` of `0x00402D70` return the equipped weapon (gang
  record +4) and armor (+5), -1 for none.
- Selector `0x61` picks the weapon candidate first (FND-AI-024 describes it).
- Selector `0x64` picks an armor: a researched item of type 3 whose Tech is at
  most the gang's own Tech (from its gang definition), whose Defense (item
  record +`0x0C`) is strictly greater than the current armor's, and whose cost
  is strictly less than cash.
- Selectors `0x65` and `0x66` read the signed 16-bit values at planning record
  +12 and +14. A slot is tried only when its value is at most 0. A successful
  Equip writes the item's cost times three into the matching value.

Selector `0x6C` opens the equipment gate. It scans the 3-by-3 neighbourhood of
the gang's sector, leaving out cells that wrap past a row but allowing linear
index 64. In scenario 0 a cell qualifies when its cached weight (selector
`0x90`, FND-AI-013) is 10 and the acting player owns it. In other scenarios a
cell qualifies when it has an owner that is not negative and not the acting
player, or its cached weight is 10. A qualifying cell opens the gate only while
the gang lacks a weapon or armor. Separately, a current sector owned by the
acting player with weight 10 opens the gate even when both slots are filled.

At index 64 the owner read lands on byte 0 of the first combat record (see
FND-AI-010), and the weight read lands on the next player's weight for sector 0
(0 for the last player).

## Interpretation

A family-1 gang upgrades its weapon, and failing that its armor, when enemies
are near or a visible hostile human is in its own sector, and then waits a
number of turns equal to three times the item's price before replacing that
slot again.

## Alternatives

Whether "cost strictly less than cash" in selector `0x64` is `<` or `<=` was
read from the instructions as strict; other handlers use an inclusive cash
test on the same selector's result (FND-AI-038). Whether the cooldown is set
when the Equip is planned or when it resolves is read as at planning.

## How to reproduce

In `0x00434080`, before the continuation at `0x004345D0`, find the calls with
selectors `0x6C`, `0x39`, `0x3A`, `0x61`, `0x64`, `0x65` and `0x66`, and the
multiply by 3 of the item cost stored back into the planning record.
