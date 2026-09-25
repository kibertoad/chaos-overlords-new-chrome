---
id: FND-DATA-009
title: DATA/DATA.Z is an InstallShield 3 archive of 459 files in five directories whose tables account for every byte
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/DATA.Z
    offset: 0x00..0x752282
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

Read as little-endian fields of `DATA/DATA.Z`:

- The header is 255 bytes. At `0x0C` a UINT16 holds 459, at `0x12` a UINT32
  holds 7,676,546 (the file's length), at `0x29` a UINT32 holds `0x74C85C`, at
  `0x31` a UINT16 holds 5, at `0x33` a UINT32 holds `0x74C8AD` and at `0x37` a
  UINT16 holds `0x59D5`.
- At `0x74C85C` there are five directory entries back to back. Each is a
  UINT16 file count, a UINT16 entry length and a UINT16 name length, then the
  name. The entry lengths are 11, 15, 20, 20 and 15 bytes, and the five entries
  end at `0x74C8AD`. The names are the empty root, `DATA`, `DATA\PX16`,
  `DATA\PX08` and `HELP`, with 3, 32, 211, 211 and 2 files.
- At `0x74C8AD` there are 459 file entries back to back, which end at
  `0x752282`, the end of the file, `0x59D5` bytes after they start. In each, a
  UINT16 at `+0x01` holds the directory index, UINT32s at `+0x03`, `+0x07` and
  `+0x0B` hold two sizes and an offset, a UINT16 at `+0x17` holds the entry's
  length, and a byte at `+0x1D` holds the length of the name that starts at
  `+0x1E`.
- The first entry's offset is `0xFF`, and each later offset is the one before
  plus the previous entry's second size, so the data blocks are contiguous. The
  last block ends at `0x74C85C`, where the directory entries start. The second
  sizes add up to 7,653,213 bytes and the first sizes to 32,320,236 bytes.
- Read as an MS-DOS date and time, the UINT16s at `0x0E` and `0x10` give 26
  April 1996, 15:54:50, the date the file carries in the GOG release. In every
  file entry the UINT16s at `+0x0F` and `+0x11` read as a valid MS-DOS date
  from 1990 to 1996 and a valid time, and the UINT32 at `+0x13` is `0x20`,
  the MS-DOS archive attribute.
- The files per directory index agree with the directory entries' counts, and
  their total agrees with the header's 459.
- The names in the root directory include the executable, a Smacker library
  and a readme.

## Interpretation

The file is an InstallShield 3 compressed archive with the layout those
archives are known to have: a 255-byte header, the compressed files back to
back, then the directory table and the file table. The first size of a file
entry is the file's size once expanded and the second the size of its
compressed block. The archive holds the game's executable, its data files and
both image sets, so it is what the original CD installer expanded. It is not
game data the program reads (FND-DATA-008).

## Alternatives

The blocks were not decompressed, so it is not shown that they expand to the
installed files, and the header bytes `0x04` to `0x0B`, `0x16` to `0x28`, `0x2D` to `0x30`
and `0x39` to `0xFE`, and the file entry bytes `+0x00` and `+0x19` to `+0x1C`,
are not interpreted.

## How to reproduce

Read the header fields at the offsets above, walk the five directory entries
from `0x74C85C` by their entry lengths, then walk the file entries from
`0x74C8AD` by the length at `+0x17`, checking that each offset follows the one
before and that the walk ends at the end of the file.
