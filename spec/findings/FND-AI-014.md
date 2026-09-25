---
id: FND-AI-014
title: The family-6 hire guards compare the previous hire role with schedule slot numbers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482160..0x00482178
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Selector `0x8F` of `0x00402D70` returns the per-player 32-bit value at
`0x00482160 + player * 4`. `0x00458FA0` copies the current hire role from
`0x00482128 + player * 4` into that array before it computes the next role.

Five scenario blocks of the hire switch hold a guard on their family-6 slot
that compares the selector-`0x8F` value with a constant: scenarios 1, 4 and 5
compare with 6, scenarios 0 and 9 with 5, scenario 2 with 2, and scenario 3
with 10. In each of these scenarios the guarded schedule slot (6, 5, 2 or 10)
writes hire role 4 (FND-AI-009), and role 4 maps to family 6 in the family
table (FND-AI-002).

## Interpretation

The value is the previous hire role, not the previous schedule slot. The guards
look meant to stop two family-6 hires in a row, but they compare a role with
the slot number of the family-6 slot. The role is 0 to 6, so scenario 3's test
against 10 is never true, and the other tests match an unrelated role (role 6,
5 or 2) instead of role 4. This is a defect of the original (BUG-AI-001).

## Alternatives

The guards could compare with the slot on purpose, if the designers meant to
avoid some other role after a family-6 hire. No reading of the roles makes
comparing with 10 in scenario 3 meaningful, which argues for a defect.

## How to reproduce

Find the case for selector `0x8F` in `0x00402D70` (a load from
`0x00482160 + player * 4`). In `0x00458FA0`, find the copy from `0x00482128`
to `0x00482160` before the hire switch, and the calls with selector `0x8F`
inside the scenario cases, each followed by a comparison with a literal.
