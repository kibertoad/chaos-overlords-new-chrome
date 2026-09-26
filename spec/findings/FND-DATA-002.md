---
id: FND-DATA-002
title: DATA/Gangs is 90 records of 156 bytes, numbered 0 to 89, with a name, a 90-byte description and 17 words
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/Gangs
    offset: 0x00..0x36D8
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

- The file is 14,040 bytes, exactly 90 records of 156 bytes.
- In every record the first 30 bytes are ASCII text of one to four words, one
  NUL byte, and spaces to the end of the 30 bytes.
- The word at `0x1E`, read as a signed 16-bit little-endian value, equals the
  record's index in every record, 0 to 89.
- The 90 bytes at `0x20..0x7A` are ASCII text padded with spaces to the end of
  the field, with no NUL byte in any record.
- The 17 words at `0x7A..0x9C`, read as signed 16-bit little-endian values,
  range as follows: `0x7A` 0 to 20, `0x7C` -3 to 7, `0x7E` -3 to 9, `0x80` 0 to
  13, `0x82` 0 to 10, `0x84` 1 to 14, `0x86` 4 to 14, `0x88` to `0x96` between
  -9 and 9, `0x98` -5 to 15, `0x9A` 0 to 12.

## Interpretation

The file is a flat table of the 90 gang definitions in the order of their
numbers. The name is a C string padded with spaces after its NUL, and the
description is a fixed-length, space-padded string without a terminator.

## Alternatives

None known for the record boundaries. What each word means is not shown by the
file alone.

## How to reproduce

Read `DATA/Gangs` as 90 records of 156 bytes and unpack `0x1E` and
`0x7A..0x9C` as signed 16-bit little-endian words.
