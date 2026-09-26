---
id: FND-DATA-010
title: The 459 blocks of DATA/DATA.Z expand to 449 installed files unchanged, and its remaining header and entry bytes are sizes or constants
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
tool: Python 3.14.7 script with a PKWARE DCL implode decoder ported from zlib contrib/blast, checked against blast's test stream
environment: null
---

## Observation

The file entries and offsets are those of FND-DATA-009.

Decompression:

- Every one of the 459 blocks decodes as a PKWARE Data Compression Library
  implode stream. All 459 start with the bytes `00 06`: literals stored
  uncoded and a dictionary of 6 bits (4096 bytes). Each stream reaches its
  end code on the last byte of its block, so the bytes consumed equal the
  entry's `+0x07` size in every entry, and each output is exactly as long as
  the entry's `+0x03` size.
- Joining the entry's directory name and file name gives a path under the
  installation. 458 of the 459 paths exist there. The one that does not is
  `README.DOC` in the root directory.
- 449 of the 458 outputs are byte for byte equal to the installed file, with
  equal size, SHA-256 and XXH3-64.
- 9 differ: `Chaos Overlords.exe` (644,608 bytes expanded, 664,576
  installed), four files of `DATA\PX16` that keep their size but differ in
  652, 18,693, 260 and 260 bytes (`PX00128`, `PX00129`, `PX05010`,
  `PX05017`), and the four files of `DATA\PX08` with the same numbers, whose
  sizes differ (expanded against installed: 83,024 and 83,274, 185,848 and
  182,538, 19,272 and 19,612, 23,522 and 23,848).
- For 449 of the 458 installed files the entry's MS-DOS date and time is
  within two hours of the file's modification time.

The header, `0x04` to `0xFE`:

- `0x04` to `0x0B` hold `3A 01 02 00 00 00 00 00`.
- The UINT32 at `0x16` is `0x01ED2AEC`, 32,320,236, the sum of the 459
  `+0x03` sizes.
- `0x1A` holds `0xFF`, `0x1B` to `0x20` are zero, `0x21` holds `0xFF` and
  `0x22` to `0x28` are zero.
- The UINT32 at `0x2D` is `0x51`, 81, the sum of the five directory entry
  lengths (11, 15, 20, 20 and 15), which is the distance from `0x74C85C` to
  the file entries at `0x74C8AD`.
- `0x39` to `0xFE`, 198 bytes, are all zero.

The entries:

- The byte at `+0x00` of every file entry is zero, and so are the four bytes
  at `+0x19`.
- Every file entry has 13 bytes after its name (its `+0x17` length minus
  `0x1E` minus the name length). They are zero in 458 entries. In the entry
  of `Chaos Overlords.exe` the fourth of them is `0x01` and the rest are zero.
- Every directory entry has 5 bytes after its name, all zero.

## Interpretation

The blocks are the game's files compressed with the PKWARE implode method,
which confirms that `DATA.Z` is the original installer's payload
(FND-DATA-005). Most installed files are the ones it held. The installed
executable and eight images are later versions than the ones the 1996 setup
expanded, and `README.DOC` was not installed.

The UINT32 at `0x16` is the expanded size of the whole archive and the
UINT32 at `0x2D` the length of the directory entries, matching the length of
the file entries at `0x37`. The other bytes are constants in this one
archive, and with no second archive to compare they have no reading beyond
their values.

## Alternatives

The values at `0x04` to `0x0B`, `0x1A` to `0x28`, file entry `+0x00` and
`+0x19` to `+0x1C`, and the byte set only for the executable's entry could be
a version, volume numbers, split offsets or flags. Only another InstallShield
3 archive, or the setup program that reads them, which is not shipped
(FND-DATA-008), would show it. Which files are later versions is read from
the installed ones differing; the archive's copies could instead be the later
ones, though the executable's larger size and the installed build being 1.1
point the other way.

## How to reproduce

Decode each block from its offset for its `+0x07` length with an implode
decoder (zlib's contrib/blast), compare the output with the installed file
at the entry's directory and name, and read the header bytes and entry bytes
listed above.
