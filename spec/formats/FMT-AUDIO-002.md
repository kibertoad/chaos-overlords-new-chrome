---
id: FMT-AUDIO-002
title: Ogg pages of the music tracks MUSIC/TrackNN.ogg
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["MUSIC/*.ogg"]
byte_order: little
size: null
text: false
definition: fmt_audio_002.ksy
evidence: [FND-AUDIO-005, FND-DATA-006]
conflicting: []
split_with: []
related: []
---

## Layout

The eight music files are ordinary Ogg Vorbis files, as RFC 3533 (Ogg) and the
Vorbis I specification define them. Each file is a sequence of Ogg pages from
its first byte to its last, carrying one logical Vorbis stream of two channels
at 44,100 samples per second (FND-AUDIO-005). This entry describes one page;
the packets inside are Vorbis data, which this spec does not describe.

`Chaos Overlords.exe` never opens these files. It plays CD audio tracks 2 to 9
through MCI (FND-AUDIO-001), and GOG's replacement `winmm.dll` answers those
requests by playing `MUSIC/TrackNN.ogg` with the same track number.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `char[4]` | `capture_pattern` | `OggS`, ASCII. | supported | FND-AUDIO-005 |
| `0x04` | 1 | `UINT8` | `version` | 0. | supported | FND-AUDIO-005 |
| `0x05` | 1 | `UINT8` | `header_type` | Page flags, split below. | supported | FND-AUDIO-005 |
| `0x05 bits 0..1` | | `bits[1]` | `continued` | 1 when the page continues a packet begun on the previous page. | supported | FND-AUDIO-005 |
| `0x05 bits 1..2` | | `bits[1]` | `first_page` | 1 on the first page of the stream. | supported | FND-AUDIO-005 |
| `0x05 bits 2..3` | | `bits[1]` | `last_page` | 1 on the last page of the stream. | supported | FND-AUDIO-005 |
| `0x05 bits 3..8` | | `bits[5]` | `unused_bits` | 0. | supported | FND-AUDIO-005 |
| `0x06` | 8 | `INT64LE` | `granule_position` | Sample position at the end of the last packet completed on the page. | supported | FND-AUDIO-005 |
| `0x0E` | 4 | `UINT32LE` | `serial` | Stream serial number, the same on every page of a file. | supported | FND-AUDIO-005 |
| `0x12` | 4 | `UINT32LE` | `sequence` | Page number within the stream, from 0. | supported | FND-AUDIO-005 |
| `0x16` | 4 | `UINT32LE` | `checksum` | CRC-32 of the page as RFC 3533 defines it. | supported | FND-AUDIO-005 |
| `0x1A` | 1 | `UINT8` | `segment_count` | Number of entries in `segment_table`. | supported | FND-AUDIO-005 |
| `0x1B` | `segment_count` | `UINT8[segment_count]` | `segment_table` | Length of each segment of the page body. | supported | FND-AUDIO-005 |
| | `body_size` | `BYTE[body_size]` | `body` | Packet data, where `body_size` is the sum of the `segment_table` entries. The first page's body is the Vorbis identification header. | supported | FND-AUDIO-005 |
| | | | | Total size 27 + segment_count + body_size | | |

## Enumerations and flags

None.

## Differences between builds

The original CD release had these tracks as CD audio; this build replaces them
with the Ogg files (BLD-GOG-EN-1.1, Compared with other builds).

## Coverage

All eight files `MUSIC/Track02.ogg` to `MUSIC/Track09.ogg` of BLD-GOG-EN-1.1
were walked page by page with a script (FND-AUDIO-005): every page starts with
`OggS`, pages cover each file exactly, and each file has one serial number.
The Kaitai definition compiles and parses all eight files, and the CRC-32 of
every one of the 10,618 pages equals its `checksum` (FND-DATA-006).

## Open questions

- The Vorbis packets are not described here; a decoder follows the Vorbis I
  specification.
