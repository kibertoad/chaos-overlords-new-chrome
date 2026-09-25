---
id: FMT-VIDEO-001
title: Smacker movies DATA/MVINTRO and DATA/MVLOGOS
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/MVINTRO", "DATA/MVLOGOS"]
byte_order: little
size: null
text: false
definition: fmt_video_001.ksy
evidence: [FND-VIDEO-001, FND-PLATFORM-006, FND-ASSET-001]
conflicting: []
split_with: []
related: []
---

## Layout

The two movies are Smacker version 2 files. The executable names them
`Data\mvIntro` and `Data\mvLogos` (FND-ASSET-001) and plays them through
`smackw32.dll` (FND-PLATFORM-006), so it never reads the fields below itself.
The header fields are laid out and named after the public description of the
Smacker format; the Huffman trees and the frame contents are left as blocks
of bytes.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `char[4]` | `signature` | `SMK2`, ASCII. | supported | FND-VIDEO-001 |
| `0x04` | 4 | `UINT32LE` | `width` | 480 pixels. | supported | FND-VIDEO-001 |
| `0x08` | 4 | `UINT32LE` | `height` | 256 lines. | supported | FND-VIDEO-001 |
| `0x0C` | 4 | `UINT32LE` | `frame_count` | 1150 in `MVINTRO`, 200 in `MVLOGOS`. | supported | FND-VIDEO-001 |
| `0x10` | 4 | `INT32LE` | `frame_rate` | -10000 in both: a negative value is the frame time in hundredths of a millisecond, so 100 ms per frame. | supported | FND-VIDEO-001 |
| `0x14` | 4 | `UINT32LE` | `flags` | 0 in both: no ring frame, no interlacing and no doubling. | supported | FND-VIDEO-001 |
| `0x18` | 28 | `UINT32LE[7]` | `audio_buffer_sizes` | Largest audio buffer per track. Track 0 is 48512 in `MVINTRO` and 24260 in `MVLOGOS`; tracks 1 to 6 are 0. | supported | FND-VIDEO-001 |
| `0x34` | 4 | `UINT32LE` | `trees_size` | Size of `trees` in bytes. | supported | FND-VIDEO-001 |
| `0x38` | 4 | `UINT32LE` | `mono_block_tree_size` | Unpacked size of the mono-block tree. | supported | FND-VIDEO-001 |
| `0x3C` | 4 | `UINT32LE` | `colour_tree_size` | Unpacked size of the colour tree. | supported | FND-VIDEO-001 |
| `0x40` | 4 | `UINT32LE` | `full_block_tree_size` | Unpacked size of the full-block tree. | supported | FND-VIDEO-001 |
| `0x44` | 4 | `UINT32LE` | `type_tree_size` | Unpacked size of the type tree. | supported | FND-VIDEO-001 |
| `0x48` | 28 | `UINT32LE[7]` | `audio_rates` | Rate and flags per audio track, split below for track 0. Tracks 1 to 6 are 0. | supported | FND-VIDEO-001 |
| `0x48 bits 0..24` | | `bits[24]` | `audio_sample_rate` | 22,050 samples per second. | supported | FND-VIDEO-001 |
| `0x48 bits 24..28` | | `bits[4]` | `audio_reserved_bits` | 0. | supported | FND-VIDEO-001 |
| `0x48 bits 28..29` | | `bits[1]` | `audio_stereo` | 1 in `MVINTRO`, 0 (mono) in `MVLOGOS`. | supported | FND-VIDEO-001 |
| `0x48 bits 29..30` | | `bits[1]` | `audio_16_bit` | 0 in both: 8-bit samples. | supported | FND-VIDEO-001 |
| `0x48 bits 30..31` | | `bits[1]` | `audio_present` | 1 in both. | supported | FND-VIDEO-001 |
| `0x48 bits 31..32` | | `bits[1]` | `audio_compressed` | 1 in both. | supported | FND-VIDEO-001 |
| `0x64` | 4 | `UINT32LE` | `reserved_64` | 0. | supported | FND-VIDEO-001 |
| `0x68` | `4 * frame_count` | `UINT32LE[frame_count]` | `frame_sizes` | Size of each frame in bytes once its two low bits are cleared. Bit 0 would mark a key frame and is clear in every frame; bit 1 is clear too. | supported | FND-VIDEO-001 |
| | `frame_count` | `UINT8[frame_count]` | `frame_types` | Per frame: 3 on the first frame (palette change and track 0 audio), 2 on most frames (track 0 audio), 0 on the last ten frames (video only). | supported | FND-VIDEO-001 |
| | `trees_size` | `BYTE[trees_size]` | `trees` | The packed Huffman trees. | supported | FND-VIDEO-001 |
| | `frame_sizes[n] & ~3` | `BYTE[frame_sizes[n] & ~3]` | `frames` | One block per frame, in order. | supported | FND-VIDEO-001 |
| | | | | Total size 104 + 5 * frame_count + trees_size + the sum of the frame sizes with their two low bits cleared | | |

## Enumerations and flags

None beyond the bit fields in the table.

## Differences between builds

None known.

## Coverage

Both files of BLD-GOG-EN-1.1 were read with a script (FND-VIDEO-001): the
header, the two tables, the trees and the frame blocks add up exactly to each
file's size, and every value in the table was read from the files. The
contents of `trees` and of the frame blocks were not decoded. The Kaitai
definition has not been compiled or run against the files.

## Open questions

- How the trees and the frames decode into pixels and samples is left to the
  public description of the Smacker format, which is not yet an entry of this
  spec. The decoding inside `smackw32.dll` has not been examined.
- The ring-frame case (`flags` bit 0, which adds one entry to both tables)
  does not occur in the shipped files.
