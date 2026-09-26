---
id: FND-AUDIO-004
title: The 28 sound files are two-chunk RIFF WAVE files of 8-bit mono PCM at 22,050 Hz
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/SND00200
    offset: 0x00..0x1572
  - build: BLD-GOG-EN-1.1
    file: DATA/SND00502
    offset: 0x00..0xADB2
  - build: BLD-GOG-EN-1.1
    file: DATA/Snd00518
    offset: 0x00..0xAD2C
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

`DATA` holds 28 files whose names start with `SND` or `Snd`: `SND00200` to
`SND00204`, `Snd00205` to `Snd00208`, `SND00500` to `SND00517` and
`Snd00518`. Each was walked as a RIFF file, chunk by chunk.

Every file starts with `RIFF`, a 32-bit little-endian size and `WAVE`, and
holds exactly two chunks: `fmt ` at `0x0C` and `data` at `0x24`. The `fmt `
chunk is 16 bytes long and holds, in every file, format tag 1, 1 channel,
22,050 samples per second, 22,050 bytes per second, block alignment 1 and 8
bits per sample. The `data` chunk's samples start at `0x2C`.

In every file the RIFF size equals 36 plus the `data` size. In 22 files the
`data` size is even and the file ends right after the samples. In six files
the `data` size is odd and one more byte follows the samples, so the file is
one byte longer than the RIFF size plus 8:

| File | `data` size | File size |
|---|---|---|
| `SND00200` | 5445 | 5490 |
| `SND00201` | 5331 | 5376 |
| `SND00204` | 3633 | 3678 |
| `SND00502` | 44421 | 44466 |
| `SND00503` | 44153 | 44198 |
| `SND00507` | 45077 | 45122 |

The smallest file is `SND00202` (966 bytes) and the largest `SND00508` (48,044
bytes).

## Interpretation

Read against the RIFF and WAVE descriptions Microsoft published: these are
plain PCM WAVE files, unsigned 8-bit mono at 22,050 Hz. The extra byte after an
odd-length `data` chunk is the pad byte RIFF requires, and the RIFF size leaves
it out.

## Alternatives

None. The executable does not parse the files itself; it loads them and hands
them to `PlaySoundA` (FND-AUDIO-003).

## How to reproduce

For each `DATA/SND*` and `DATA/Snd*` file, read the 12-byte RIFF header, then
walk the chunks (8-byte id and size, then the body padded to an even length)
to the end of the file, and print the `fmt ` fields and the sizes.
