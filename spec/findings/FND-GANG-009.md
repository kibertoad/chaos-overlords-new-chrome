---
id: FND-GANG-009
title: The compact gang information panel 0x00455B6B is opened only from the Attack, Equip, Research, Sell and Give panels, with a copy of a live gang record
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00455B6B..0x00456F7B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043C885
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043C9DD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043ECD8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004434DA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00444A56
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004466CF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00446807
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

- `fn_00455B6B` has seven call sites: two in the Attack handler `fn_0043B290`
  (`0x0043C885`, `0x0043C9DD`), one in the Equip handler `fn_0043DAD9`
  (`0x0043ECD8`), one in the Research handler `fn_004427FA` (`0x004434DA`),
  one in the Sell handler `fn_00443BBD` (`0x00444A56`) and two in the Give
  handler `fn_00445A4F` (`0x004466CF`, `0x00446807`). No other code refers to
  its address. The Hire handler `fn_00416C75` does not call it
  (FND-HIRE-008).
- Each call site copies a 32-byte gang record onto the stack with a string
  move of eight words and pushes 1 before it; at `0x0043ECD8` the record is
  the Equip handler's local copy of the acting gang.
- The panel takes the gang definition from record byte 1 and draws the
  definition's name and three 30-character rows of its description, the
  definition's portrait, the definition's Upkeep negated (`0x00455E13`) and
  its field at offset `0x82` (`0x00455E5F`). It draws record byte 3 (Force)
  with `fn_00414187` when it is nonzero and the string at `0x0048776C` when it
  is 0 (`0x00455D78`). It draws the fourteen record bytes at offsets `0x12` to
  `0x1F`, four of them with `fn_00414187` (`0x00455EAF` to `0x00455F69`) and
  ten with `fn_004142E7` (`0x00455FA5` to `0x004561C1`). Only while the byte
  at `0x0048784C` is nonzero (`0x004561DE`) does it also draw the definition's
  own values at offsets `0x7E`, `0x80`, `0x84` and onward in a further column.

## Interpretation

The panel of FND-GANG-002 is a compact information panel for a gang that
exists: an order panel opens it when its gang portrait, or a target's or
recipient's portrait, is double-clicked. It shows that gang's current Force
and effective statistics, with the definition's Upkeep and Tech Level, and the
definition's base values beside them when `pref_base_stats` is set. A hire
offer is never shown on it; the hire dock's double-click opens the live-gang
panel of FND-GANG-008 instead. FND-GANG-002's reading of it as the panel for a
hire offer's definition does not match the callers.

## Alternatives

- What the argument 1 pushed after the record selects was not traced.

## How to reproduce

List the callers of `0x00455B6B`. At each call site, find the eight-word copy
into the stack and the push of 1. In `fn_00455B6B`, follow the reads of the
record's bytes 1 and 3 and of the stack bytes of the record's offsets `0x12`
to `0x1F`, the test at `0x00455D78`, and the test of `0x0048784C` at
`0x004561DE`.
