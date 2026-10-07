---
id: FND-AI-076
title: Family 3 stores -1 in the focus after every action it plans after None, Control, Equip or Heal
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00435BD0..0x00435F12
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043A1D0..0x0043B281
tool: Ghidra 12.1.3
environment: null
---

## Observation

In the family-3 handler `0x00435BD0` (FND-AI-001), the case for a previous
action of None, Control, Equip or Heal plans one of four actions, and every
one of them reaches `0x00435EEA`:

| Action | Last focus store of the branch | Way to `0x00435EEA` |
|---|---|---|
| Heal | `0x00435C93`, -1 | `JMP` at `0x00435C9D` |
| Influence | `0x00435DD1`, the gang's sector | `JMP` at `0x00435DD9` |
| Control | none | `JMP` at `0x00435E31` |
| Move | `0x00435EE0`, -1 | falls through |

The instructions at `0x00435EEA..0x00435F03` compute the auxiliary record of
the gang (`0x0048C0BA + player * 0x46E + slot * 14`, FND-AI-044) and store
-1 in its `focus` at `0x00435F03`. The `JMP` at `0x00435F0D` then leaves the
case for the handler's common tail at `0x00436B41`.

The family-5 handler `0x0043A1D0` has the same case without that store: each
branch leaves the case on its own.

## Interpretation

A family-3 gang planning after None, Control, Equip or Heal ends the pass
with -1 in its focus, whatever it planned: the sector stored by the
Influence branch is overwritten at once, and Control, which stores no focus
of its own, also leaves -1. This corrects FND-AI-074, which pairs
`0x00435F03` with the Move branch and reads family 3's Influence as keeping
the gang's sector and its Control as leaving the focus unchanged. Those
readings hold for the other cases of family 3 and for family 5.

## Alternatives

`0x00435EEA` could be the target of a jump from outside the case. The
program's references to it are the three jumps listed, all inside the case,
and the only other way in is the fall-through from the Move branch.

## How to reproduce

Disassemble `0x00435BD0..0x00435F12`, list the references to `0x00435EEA`,
and read the store at `0x00435F03`. EXP-TURN-054 shows the result in the running original.
