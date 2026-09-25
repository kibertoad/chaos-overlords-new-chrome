---
id: FMT-AUDIO-001
title: Sound effect files DATA/SNDnnnnn
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/SND*", "DATA/Snd*"]
byte_order: little
size: null
text: false
definition: fmt_audio_001.ksy
evidence: [FND-AUDIO-004, FND-ASSET-001, FND-AUDIO-003, FND-DATA-006, FND-EXE-004]
conflicting: []
split_with: []
related: []
---

## Layout

Each of the 28 sound files is a RIFF WAVE file with exactly two chunks, `fmt `
and `data`, in that order (FND-AUDIO-004). The executable names them from the
template `data\snd00000` (FND-ASSET-001) and plays them from memory with
`PlaySoundA` (FND-AUDIO-003).

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `char[4]` | `riff_id` | `RIFF`, ASCII. | supported | FND-AUDIO-004 |
| `0x04` | 4 | `UINT32LE` | `riff_size` | 36 plus `data_size`: the file size minus 8, or minus 9 in the six files whose `data_size` is odd. | supported | FND-AUDIO-004 |
| `0x08` | 4 | `char[4]` | `wave_id` | `WAVE`, ASCII. | supported | FND-AUDIO-004 |
| `0x0C` | 4 | `char[4]` | `fmt_id` | `fmt `, ASCII, ending in a space. | supported | FND-AUDIO-004 |
| `0x10` | 4 | `UINT32LE` | `fmt_size` | 16. | supported | FND-AUDIO-004 |
| `0x14` | 2 | `UINT16LE` | `format_tag` | 1, PCM. | supported | FND-AUDIO-004 |
| `0x16` | 2 | `UINT16LE` | `channels` | 1, mono. | supported | FND-AUDIO-004 |
| `0x18` | 4 | `UINT32LE` | `sample_rate` | 22,050 samples per second. | supported | FND-AUDIO-004 |
| `0x1C` | 4 | `UINT32LE` | `byte_rate` | 22,050 bytes per second. | supported | FND-AUDIO-004 |
| `0x20` | 2 | `UINT16LE` | `block_align` | 1. | supported | FND-AUDIO-004 |
| `0x22` | 2 | `UINT16LE` | `bits_per_sample` | 8. | supported | FND-AUDIO-004 |
| `0x24` | 4 | `char[4]` | `data_id` | `data`, ASCII. | supported | FND-AUDIO-004 |
| `0x28` | 4 | `UINT32LE` | `data_size` | Number of samples. | supported | FND-AUDIO-004 |
| `0x2C` | `data_size` | `UINT8[data_size]` | `samples` | Unsigned 8-bit PCM samples, 128 being silence. | supported | FND-AUDIO-004 |
| | `1 if data_size % 2 == 1` | `BYTE[1]` | `pad` | The RIFF pad byte after an odd-length `data` chunk. Present in `SND00200`, `SND00201`, `SND00204`, `SND00502`, `SND00503` and `SND00507`. | supported | FND-AUDIO-004 |
| | | | | Total size 44 + data_size + data_size % 2 | | |

## Enumerations and flags

None.

## Differences between builds

None known.

## Coverage

All 28 sound files of BLD-GOG-EN-1.1 (`DATA/SND00200` to `DATA/SND00204`,
`DATA/Snd00205` to `DATA/Snd00208`, `DATA/SND00500` to `DATA/SND00517`,
`DATA/Snd00518`) were read with a script (FND-AUDIO-004). Every field holds the
value in the table and each file ends exactly after `samples` and `pad`. The
Kaitai definition compiles and parses all 28 files (FND-DATA-006).

## Open questions

- The six files with an odd `data_size` carry a `riff_size` one smaller than
  the RIFF rules ask for. Windows plays them regardless; whether the game ever
  checks the size itself has not been traced.
