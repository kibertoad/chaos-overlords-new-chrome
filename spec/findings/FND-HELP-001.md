---
id: FND-HELP-001
title: The help file's context tree holds 80 hashed names, covers every contents target, and defines no numeric contexts
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0xE301..0xEB30
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0xBF34..0xBF3F
  - build: BLD-GOG-EN-1.1
    file: HELP/CHAOS.CNT
    offset: 0x00..0x70E
tool: WinHelp container parser written from the public description of the format
environment: null
---

## Observation

- The internal file `|CONTEXT` (`0xE301..0xEB30`) is a B+ tree with 80
  entries. Each leaf entry is an unsigned 32-bit hash of a context name
  followed by a signed 32-bit topic offset.
- Every one of the 59 topic targets named in `HELP/CHAOS.CNT` hashes to an
  entry of that tree. For example, `INTRO` hashes to `0x053D9A5C`, `CITYVIEW`
  to `0x86EE9810` and `ITEMINFO` to `0xEB824CED`.
- The internal file `|CTXOMAP` (`0xBF34..0xBF3F`) starts with an entry count
  of 0.

## Interpretation

Help topics are reached by context name, through its hash. The file defines
no numeric context IDs of the kind a `[MAP]` section produces, so the
executable cannot open a topic by number. A context offset marks a place in
the topic text and need not be the start of a topic.

## Alternatives

None known.

## How to reproduce

Read the WinHelp directory at offset `0xE42` of `HELP/Chaos.hlp` to find the
internal files `|CONTEXT` and `|CTXOMAP` (FND-HELP-003), walk the context tree,
and hash each target after `=` in `HELP/CHAOS.CNT` with the WinHelp context
hash.
