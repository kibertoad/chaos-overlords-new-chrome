---
id: FND-DATA-001
title: DATA/SITES is 22 records of 62 bytes, numbered 0 to 21, with a special-site word of 0 to 3 at 0x3C
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/SITES
    offset: 0x00..0x554
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

- The file is 1,364 bytes, exactly 22 records of 62 bytes, with nothing before
  the first record or after the last.
- In every record the first 20 bytes are ASCII text of one to three words, then
  one NUL byte, then spaces to the end of the 20 bytes. No record uses a byte
  outside `0x20..0x7E` in its text.
- The 21 words from `0x14` to `0x3E` read as signed 16-bit little-endian
  values. The word at `0x14` equals the record's index in every record, 0 to
  21. The other words range from -2 to 16.
- The word at `0x3C` is 0 in 19 records and 1, 2 and 3 in one record each.
- The words at `0x20` (all 0), `0x2A` (all 0), `0x34` (all 0) and `0x3A` (all 0)
  are zero in every record.

## Interpretation

The file is a flat table of the 22 site definitions, in the order of their
numbers. The name field is a C string padded with spaces after its NUL. The
values at `0x3C` form a small enumeration with three special sites.

## Alternatives

A 62-byte record is also consistent with other splits of the 42 bytes after
the name. The word-by-word reading is the one that puts the record number at
`0x14` and keeps every value small; SRC-RECHAOS-3561D41 describes the same
split.

## How to reproduce

Read `DATA/SITES` as 22 records of 62 bytes and unpack each record's bytes
`0x14..0x3E` as 21 signed 16-bit little-endian words.
