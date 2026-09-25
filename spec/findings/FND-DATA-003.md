---
id: FND-DATA-003
title: DATA/ITEMS is 64 records of 166 bytes, 53 items numbered 0 to 52 and 11 blank records of type 99
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/ITEMS
    offset: 0x00..0x2980
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

- The file is 10,624 bytes, exactly 64 records of 166 bytes.
- Records 0 to 52 (`0x00..0x225E`) carry the number of their index in the word
  at `0x1E`. Their first 30 bytes are ASCII text, one NUL byte and spaces, and
  their 90 bytes at `0x20..0x7A` are ASCII text padded with spaces and no NUL.
- Records 53 to 63 (`0x225E..0x2980`) hold 30 spaces, a word of 0 at `0x1E`,
  90 spaces, the value 99 in the word at `0x7A` and 0 in every other word.
- The word at `0x7A` holds 0 in 6 records, 1 in 6, 2 in 12, 3 in 14, 4 in 15
  and 99 in 11. The real items are grouped by it: records 0 to 11 hold only 0
  and 1, records 12 to 23 hold 2, 24 to 37 hold 3, and 38 to 52 hold 4.
- The other 21 words from `0x7C` to `0xA6` read as signed 16-bit values. Among
  them, `0x7E` ranges from 0 to 45 and `0x80` from 0 to 10. The last four words
  range over `0x9E` 0 to 26, `0xA0` 0 to 19, `0xA2` 0 to 17 and `0xA4` 0 to 14.
- The words at `0x96`, `0x9A` and `0x9C` are 0 in every record.

## Interpretation

The file is a table of 64 item slots, of which the first 53 hold items and the
last 11 are blank records whose type word is 99. The type word groups the
items into categories. The last four words' ranges match the numbers of the
`PX070xx` (0 to 26) and `PX071xx` (0 to 19) animation strips, the `SND005xx`
weapon sounds (0 to 17) and the 15 frames of a `PX04xxx` rotation strip.

## Alternatives

None known for the record boundaries. The matching of the last four words to
resources is by range only here; FND-AUDIO-002 shows the executable reading
the sound word and the last word.

## How to reproduce

Read `DATA/ITEMS` as 64 records of 166 bytes and unpack `0x1E` and
`0x7A..0xA6` as signed 16-bit little-endian words.
