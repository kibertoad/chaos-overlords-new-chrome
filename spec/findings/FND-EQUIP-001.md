---
id: FND-EQUIP-001
title: A Factory lowers an item's price by its cost divided by three, truncated
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474998
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004749B2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004749E1..0x004749F3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F36D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044D498
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044DC36
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044D1BB
tool: Ghidra 12.1.3
environment: null
---

## Observation

- In the Equip branch of the whole-turn resolver `fn_00472775`, the
  instruction at `0x00474998` loads the chosen item's Cost as a signed value.
  At `0x004749B2` the code reads the byte at offset `0x0E` of the sector
  record. When that byte is set and the sector's owner byte at offset `0x00`
  equals the buying player, the instructions at `0x004749E1..0x004749F3` divide the Cost
  by three with signed integer division and subtract the quotient from the
  Cost.
- The byte at offset `0x0E` of the sector record has four direct reads:
  `0x0043F36D`, `0x0044D498`, `0x0044DC36` and the resolver read at
  `0x004749B2`.
- The function at `0x0044D1BB` computes the same `cost - cost / 3`.

## Interpretation

The Factory price is `Cost - trunc(Cost / 3)`: the discount is a third of the
cost rounded toward zero, so the price is rounded toward the full cost. It is
neither a 30 percent discount nor `floor(Cost * 70 / 100)`. Item costs are not
negative, so the signed division rounds down. With a Factory, an item whose
Cost is 11 is bought for 8, and one whose Cost is 12 is also bought for 8. The discount applies only to the
owner of a sector whose Factory is complete, when the buying gang stands in
it.

## Alternatives

- `0x0044D1BB` is also the entry of the Financial panel handler
  (FND-FINANCE-001); the Factory price there is presumably the panel's
  Equipment projection. `0x0043F36D` lies after the Equip list builder
  `fn_0043F136` (FND-EQUIP-006) and is presumably its price column. Neither
  containing function has been confirmed.
- Which sector the resolver reads, the gang's current one, is assumed from the
  manual's description of the Factory; the observation does not name the
  index.

## How to reproduce

In `fn_00472775`, find the case for action 5. The load at `0x00474998`, the read
of offset `0x0E` at `0x004749B2` and the division by 3 from `0x004749E1` follow
each other. Cross-references to offset `0x0E` of the sector table at
`0x004A08E8` give the other three reads.
