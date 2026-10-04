---
id: FND-AI-075
title: Family 12 stores the focus with every action it plans and a Move's destination as the coverage sector
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004353A0..0x00435BC0
tool: Ghidra 12.1.3
environment: null
---

## Observation

The family-12 handler `0x004353A0` (FND-AI-001) stores a 16-bit value into
the auxiliary record of the gang it plans for (`0x0048C0B0 + player * 0x46E +
slot * 14`, FND-AI-044) after each action:

| Action | Store | Value |
|---|---|---|
| Equip of a weapon | `0x0043573F` | -1 in `focus` (`+0x0A`) |
| Equip of an armor | `0x0043588D` | -1 in `focus` |
| Equip of a miscellaneous item | `0x0043598F` | -1 in `focus` |
| Heal | `0x00435A25` | -1 in `focus` |
| Move | `0x00435AF2`, `0x00435B30` | -1 in `focus`, and the destination byte the sector selector `0x00408642` returned, sign extended, in `coverage_sector` (`+0x0C`) |
| Attack | `0x004355F3` | the gang's sector in `focus` |

The sector the Attack branch stores is the value selector `0x5A` returned at
the handler's entry. No other store in the handler writes either value; the
Greed Terminate branch that follows at `0x00435B38` writes neither.

## Interpretation

Family 12 keeps its focus in step with the action, as families 3 and 5 do
(FND-AI-074): the sector of the fight it starts, -1 otherwise. Its Move keeps
the destination in `coverage_sector`, which stays in the record when a later
pass gives the gang another family without the family flag, so a gang that
moved as family 12 carries that sector into its next family.

## Alternatives

The table pairs each store with the branch the decompiled control flow puts it
in. The Move branch stores both values after the action and the target, so a
store reached from two branches would have shown in that flow.

## How to reproduce

List the references to `0x0048C0BA` and `0x0048C0BC` inside `0x004353A0`, and
read the branch each store closes.
