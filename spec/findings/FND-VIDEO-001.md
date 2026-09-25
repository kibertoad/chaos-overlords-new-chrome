---
id: FND-VIDEO-001
title: MVINTRO and MVLOGOS are Smacker version 2 files of 480 by 256 at 10 frames per second whose frame table covers the file
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/MVINTRO
    offset: 0x00..0x831D54
  - build: BLD-GOG-EN-1.1
    file: DATA/MVLOGOS
    offset: 0x00..0x1BF5B4
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

Both files start with `SMK2` and a header of 104 bytes read as little-endian
32-bit values:

| Offset | `MVINTRO` | `MVLOGOS` |
|---|---|---|
| `0x04` width | 480 | 480 |
| `0x08` height | 256 | 256 |
| `0x0C` frame count | 1150 | 200 |
| `0x10` frame rate, signed | -10000 | -10000 |
| `0x14` flags | 0 | 0 |
| `0x18` largest audio buffer, track 0 | 48512 | 24260 |
| `0x1C..0x34` largest audio buffer, tracks 1 to 6 | 0 | 0 |
| `0x34` Huffman tree data size | 109858 | 66480 |
| `0x38` | 169200 | 111840 |
| `0x3C` | 98824 | 46208 |
| `0x40` | 127000 | 79480 |
| `0x44` | 6248 | 3640 |
| `0x48` audio rate and flags, track 0 | `0xD0005622` | `0xC0005622` |
| `0x4C..0x64` tracks 1 to 6 | 0 | 0 |
| `0x64` | 0 | 0 |

After the header come one 32-bit size per frame, one type byte per frame, the
tree data, and the frames. The header, the two tables, the tree data and the
frame sizes with their two low bits cleared add up exactly to each file's
size. No frame size has either of its two low bits set. The type bytes are 3
for the first frame, 2 for most frames and 0 for the last ten frames of each
file.

## Interpretation

Read against the public description of the Smacker format: both movies are
480 by 256, and a negative frame rate of -10000 means 10000 / 100 = 100
milliseconds per frame, so `MVINTRO` runs 115 seconds and `MVLOGOS` 20. Flags
0 means no ring frame and no interlacing or doubling. The four sizes from
`0x38` are the unpacked sizes of the mono-block, colour, full-block and type
trees. Only audio track 0 is used: the rate field's low 24 bits give 22,050 Hz,
bit 31 compressed data, bit 30 data present, bit 29 clear for 8-bit samples,
and bit 28 stereo for `MVINTRO` and clear (mono) for `MVLOGOS`. A frame type
of 2 carries audio for track 0, 3 adds a palette change, and 0 carries video
only. No frame is marked as a key frame.

## Alternatives

The meanings come from the public description of the format, which is not an
entry of this spec; the executable hands the files to `smackw32.dll`
(FND-PLATFORM-006) and does not parse them itself.

## How to reproduce

Read the first 104 bytes of each file, then the frame-size and frame-type
tables, and add up the parts.
