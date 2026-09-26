---
id: FND-HELP-003
title: Chaos.hlp is a WinHelp 3.1 container with an eleven-file directory at 0xE42
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0x00..0x10
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0xE42..0x1271
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0x14E2..0x1604
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

- The file is 60,208 bytes. Its first 16 bytes, read as little-endian
  integers, are the magic `0x00035F3F`, the directory offset `0xE42`, the
  first free block -1 and the file size 60,208.
- At `0xE42` is a 9-byte internal file header (reserved size 1071, used size
  1062, flags 4) followed by a B+ tree header: magic `0x293B`, flags `0x0402`,
  page size 1024, structure string `z4`, root page 0, one page, one level and
  11 entries.
- The single leaf page lists eleven internal files and their offsets:
  `|PhrImage` `0x10`, `|PhrIndex` `0x1271`, `|SYSTEM` `0x14E2`, `|TOPIC`
  `0x1604`, `|FONT` `0xBE80`, `|CTXOMAP` `0xBF34`, `|KWDATA` `0xBF3F`,
  `|KWMAP` `0xC28C`, `|KWBTREE` `0xC2A3`, `|TTLBTREE` `0xDAD2` and `|CONTEXT`
  `0xE301`. Each internal file starts with its own 9-byte header, and its
  reserved size, counted from that header, ends it exactly where the next one
  begins; `|CONTEXT` ends at the end of the file.
- In every internal file header the used size is the reserved size minus 9.
  The flags byte is 4 in the directory's header and 0 in the eleven others.
- The leaf page at `0xE42 + 9 + 38` starts with four 16-bit values: 879
  unused bytes, 11 entries, previous page -1 and next page -1. Each entry is
  a NUL-terminated name followed by a 32-bit offset, and the entries are
  sorted by name in byte order. The page is 1024 bytes and ends at `0x1271`.
- `|SYSTEM` starts with magic `0x036C`, minor version 33, major version 1 and
  flags 4.

## Interpretation

The help file is an ordinary WinHelp 3.1 file (minor version 33) with
compressed topics (flags 4) and a phrase table. The executable opens it by the
path `.\Help\Chaos.hlp` (FND-ASSET-001) and hands it to the Windows help
viewer, so the game itself never parses it.

## Alternatives

The field names used here are those of the public description of the WinHelp
format, which is not an entry of this spec. The claim that the game passes the
file to the viewer rests on the `WinHelpA` import (FND-PLATFORM-001) and has not
been traced to its callers.

## How to reproduce

Read the first 16 bytes of `HELP/Chaos.hlp`, then the internal file header and
B+ tree header at `0xE42`, then the leaf page at `0xE42 + 9 + 38`.
