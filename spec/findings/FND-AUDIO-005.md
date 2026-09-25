---
id: FND-AUDIO-005
title: The eight music tracks are Ogg files of one Vorbis stream each, stereo at 44,100 Hz
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: MUSIC/Track02.ogg
    offset: 0x00..0x3CB3B9
  - build: BLD-GOG-EN-1.1
    file: MUSIC/Track09.ogg
    offset: 0x00..0x1C8A35
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

`MUSIC` holds `Track02.ogg` to `Track09.ogg`. Each file was walked as a chain
of pages: a page starts with `OggS`, has a 27-byte header whose last byte at
`0x1A` counts the entries of the segment table that follows, and ends after a
body whose length is the sum of those entries.

| File | Size | Pages |
|---|---|---|
| `Track02.ogg` | 3,978,169 | 938 |
| `Track03.ogg` | 3,993,167 | 941 |
| `Track04.ogg` | 6,923,598 | 1633 |
| `Track05.ogg` | 4,601,441 | 1085 |
| `Track06.ogg` | 10,619,761 | 2501 |
| `Track07.ogg` | 6,409,140 | 1509 |
| `Track08.ogg` | 6,661,019 | 1568 |
| `Track09.ogg` | 1,870,389 | 443 |

In every file each page starts with `OggS`, the last page ends exactly at the
end of the file, and all pages carry the same serial number. The first page's
body starts at `0x1C` with the byte 1 and `vorbis`, followed by a 32-bit
version 0, a channel count of 2 and a 32-bit rate of 44,100.

The page checksums were not verified.

## Interpretation

Read against RFC 3533 and the Vorbis I specification: each file is one logical
Ogg Vorbis stream of stereo audio at 44,100 samples per second. The files are
GOG's replacement for the CD audio tracks 2 to 9 that the executable asks MCI
to play (FND-AUDIO-001); the executable itself never names them.

## Alternatives

None for the container. Which track number plays in which game situation is
the audio rules' business, not this finding's.

## How to reproduce

For each file, from offset 0: check `OggS`, read the segment count at page
offset `0x1A`, add the segment table's entries, advance by 27 plus the count
plus the sum, and repeat until the end of the file. Then read the Vorbis
identification header in the first page's body.
