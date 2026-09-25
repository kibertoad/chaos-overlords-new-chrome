---
id: FND-DATA-006
title: The Kaitai definitions of the shipped file formats parse every shipped file to its last byte with the documented field values
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/SITES
    offset: 0x00..0x553
  - build: BLD-GOG-EN-1.1
    file: DATA/Gangs
    offset: 0x00..0x36D7
  - build: BLD-GOG-EN-1.1
    file: DATA/ITEMS
    offset: 0x00..0x297F
  - build: BLD-GOG-EN-1.1
    file: DATA/CLT00002
    offset: 0x00..0x3AF
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00128
    offset: 0x00..0x8FC35
  - build: BLD-GOG-EN-1.1
    file: DATA/PX08/PX06008
    offset: 0x00..0xA161
  - build: BLD-GOG-EN-1.1
    file: DATA/SND00200
    offset: 0x00..0x1571
  - build: BLD-GOG-EN-1.1
    file: MUSIC/Track02.ogg
    offset: 0x00..0x3CB3B8
  - build: BLD-GOG-EN-1.1
    file: DATA/MVINTRO
    offset: 0x00..0x831D53
  - build: BLD-GOG-EN-1.1
    file: DATA/MVLOGOS
    offset: 0x00..0x1BF5B3
  - build: BLD-GOG-EN-1.1
    file: HELP/Chaos.hlp
    offset: 0x00..0xEB2F
tool: kaitai-struct-compiler 0.11 (Python target) with the kaitaistruct 0.11 Python runtime, and a Python 3.14.7 script
environment: null
---

## Observation

Every `.ksy` file of the shipped formats (`fmt_data_001` to `fmt_data_004`,
`fmt_gfx_001` to `fmt_gfx_003`, `fmt_audio_001`, `fmt_audio_002`,
`fmt_video_001`, `fmt_help_001`, `fmt_save_001`, `fmt_save_002`) compiles with
kaitai-struct-compiler 0.11. The only messages are style warnings about field
names. `fmt_audio_002` and `fmt_video_001`, which size a repeated field with
`_index`, also compile for every other target the compiler offers.
`fmt_gfx_002` named a custom process routine for its RLE8 data; with the
routine removed from the definition the field holds the stored bytes, and it
compiles and runs without code of its own.

The generated Python readers were run on every shipped file of each format:

| Definition | Files | Result |
|---|---|---|
| `fmt_data_001` | `DATA/SITES` | 22 records, ends on the last byte. `id` equals the record index in all 22. `special` holds only 0 to 3. `combat`, `control`, `blade` and `martial_arts` are 0 in every record. |
| `fmt_data_002` | `DATA/Gangs` | 90 records, ends on the last byte. `id` equals the record index in all 90. |
| `fmt_data_003` | `DATA/ITEMS` | 64 records, ends on the last byte. `id` equals the index in records 0 to 52 and is 0 in records 53 to 63. `type` holds 0 to 4 and 99. `blade`, `fighting` and `martial_arts` are 0 in every record. |
| `fmt_data_004` | `DATA/CLT00002` | 236 entries, ends on the last byte. Every red, green and blue level is a multiple of 17, and the fourth byte is 4 in every entry. |
| `fmt_gfx_001` | all 215 files under `DATA/PX16/` | Each ends on its last byte. `file_size` equals the file's length, `pixel_offset` 54, `header_size` 40, `width` and `height` 0, `planes` 255, `bit_count` 16, `compression` 0, `image_size` the file length minus 54, the other fields 0. Bit 15 is clear in every pixel. |
| `fmt_gfx_002`, `fmt_gfx_003` | all 214 files under `DATA/PX08/` | Each ends on its last byte. `pixel_offset` 1078, `header_size` 40, `width` and `height` 0, `planes` 255, `bit_count` 8, `compression` 1 in 207 files and 0 in 7, `image_size` the file length minus 1078, both pixels-per-metre fields 2835, `colors_used` and `colors_important` 0. Each has 256 palette entries whose `reserved_03` is 0. |
| `fmt_audio_001` | the 28 files `DATA/SND*` and `DATA/Snd*` | Each ends on its last byte. `fmt_size` 16, `format_tag` 1, `channels` 1, `sample_rate` and `byte_rate` 22050, `block_align` 1, `bits_per_sample` 8. `riff_size` is 36 plus `data_size` in all 28; `data_size` is odd in 6 of them. |
| `fmt_audio_002` | `MUSIC/Track02.ogg` to `MUSIC/Track09.ogg` | 938, 941, 1633, 1085, 2501, 1509, 1568 and 443 pages, ending on each file's last byte. One serial number per file, `sequence` counting from 0, `first_page` set on the first page only, `last_page` on the last page only, `version` and `unused_bits` 0, each `body` element as long as its segment table entry. A separate script computed the RFC 3533 CRC-32 of every page (checksum field zeroed); it equals `checksum` on all 10,618 pages. |
| `fmt_video_001` | `DATA/MVINTRO`, `DATA/MVLOGOS` | Both end on their last byte, every frame block as long as its `frame_sizes` entry with the two low bits cleared (no entry has either bit set). Width 480, height 256, frame counts 1150 and 200, `frame_rate` -10000, `flags` 0, audio buffer sizes 48512 and 24260 for track 0 and 0 for the others, track 0 at 22,050 samples per second, present and compressed, 8-bit, stereo in `MVINTRO` and mono in `MVLOGOS`, tracks 1 to 6 zero, `reserved_64` 0. `frame_types`: 3 once, 0 ten times, 2 on the other frames. |
| `fmt_help_001` | `HELP/Chaos.hlp` | `directory_offset` 3650 (`0xE42`), `first_free_block` -1, `file_size` 60208, the file's length. Directory header `reserved_size` 1071, `used_size` 1062, `file_flags` 4. B+ tree `btree_flags` 1026 (`0x0402`), `page_size` 1024, `structure` `z4`, `must_be_zero` 0, `page_splits` 0, `root_page` 0, `must_be_minus_one` -1, one page, one level, 11 entries. Leaf `unused_bytes` 879, `entry_count` 11, both page links -1, and eleven entries naming the internal files. |

No save file of either form exists in the build or in the repository, so
`fmt_save_001` and `fmt_save_002` were compiled but not run.

## Interpretation

The format entries and their definitions agree field for field with the files
of this build, and the Ogg pages are intact. The definitions can be used as
they stand to read the shipped files. The RLE8 data stays encoded after
parsing; decoding it is RULE-GFX-001's job.

## Alternatives

None known. The runs check the definitions against this build's files only;
they say nothing about fields the files never exercise, such as nonzero
`flags` in a movie.

## How to reproduce

Compile each definition in `spec/formats/` with
`kaitai-struct-compiler -t python`, install the `kaitaistruct` Python runtime,
parse each shipped file with the generated class, check that the stream
position after parsing equals the file length, and count the values of each
field.
