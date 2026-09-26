---
id: FMT-DATA-005
title: Compressed archive DATA/DATA.Z
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/DATA.Z"]
byte_order: little
size: 7676546
text: false
definition: fmt_data_005.ksy
evidence: [FND-DATA-005, FND-DATA-008, FND-DATA-009, FND-DATA-010]
conflicting: []
split_with: []
related: []
---

## Layout

The header:

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `UINT32LE` | `signature` | `0x8C655D13` (bytes `13 5D 65 8C`), the signature of an InstallShield 3 compressed archive. | supported | FND-DATA-005, FND-DATA-009 |
| `0x04` | 8 | `BYTE[8]` | `unk_04` | `3A 01 02 00 00 00 00 00`. | supported | FND-DATA-010 |
| `0x0C` | 2 | `UINT16LE` | `file_count` | Number of file entries, 459. | supported | FND-DATA-009 |
| `0x0E` | 2 | `UINT16LE` | `date` | MS-DOS date of the archive, 26 April 1996. | supported | FND-DATA-009 |
| `0x10` | 2 | `UINT16LE` | `time` | MS-DOS time of the archive, 15:54:50. | supported | FND-DATA-009 |
| `0x12` | 4 | `UINT32LE` | `archive_size` | Length of the whole file. | supported | FND-DATA-009 |
| `0x16` | 4 | `UINT32LE` | `expanded_total` | Sum of the file entries' `expanded_size`, 32,320,236. | supported | FND-DATA-010 |
| `0x1A` | 1 | `BYTE` | `unk_1a` | `0xFF`. | supported | FND-DATA-010 |
| `0x1B` | 6 | `BYTE[6]` | `unk_1b` | Zero. | supported | FND-DATA-010 |
| `0x21` | 1 | `BYTE` | `unk_21` | `0xFF`. | supported | FND-DATA-010 |
| `0x22` | 7 | `BYTE[7]` | `unk_22` | Zero. | supported | FND-DATA-010 |
| `0x29` | 4 | `UINT32LE` | `dir_table_offset` | Offset of the directory entries, which follow the last data block. | supported | FND-DATA-009 |
| `0x2D` | 4 | `UINT32LE` | `dir_table_size` | Length of the directory entries, 81. | supported | FND-DATA-010 |
| `0x31` | 2 | `UINT16LE` | `dir_count` | Number of directory entries, 5. | supported | FND-DATA-009 |
| `0x33` | 4 | `UINT32LE` | `file_table_offset` | Offset of the file entries, right after the directory entries. | supported | FND-DATA-009 |
| `0x37` | 2 | `UINT16LE` | `file_table_size` | Length of the file entries, which end the file. | supported | FND-DATA-009 |
| `0x39` | 198 | `BYTE[198]` | `unk_39` | Zero. | supported | FND-DATA-010 |
| `0xFF` | | `BYTE[]` | `blocks` | The compressed files back to back, in file-table order, up to `dir_table_offset`. Each is a PKWARE Data Compression Library implode stream that starts `00 06` (literals uncoded, 4096-byte dictionary) and ends on its last byte. | supported | FND-DATA-009, FND-DATA-010 |

A directory entry, `dir_count` of them from `dir_table_offset`:

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 2 | `UINT16LE` | `file_count` | Files whose `dir_index` is this entry's index. | supported | FND-DATA-009 |
| `0x02` | 2 | `UINT16LE` | `entry_size` | Length of this entry; the next starts that many bytes on. | supported | FND-DATA-009 |
| `0x04` | 2 | `UINT16LE` | `name_length` | Length of `name`. | supported | FND-DATA-009 |
| `0x06` | `name_length` | `CHAR[]` | `name` | The directory's path, empty for the root. | supported | FND-DATA-009 |
| `0x06 + name_length` | 5 | `BYTE[5]` | `unk_tail` | Zero in every entry. | supported | FND-DATA-010 |

A file entry, `file_count` of them from `file_table_offset`:

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `BYTE` | `unk_00` | Zero in every entry. | supported | FND-DATA-010 |
| `0x01` | 2 | `UINT16LE` | `dir_index` | Index of the file's directory entry. | supported | FND-DATA-009 |
| `0x03` | 4 | `UINT32LE` | `expanded_size` | The file's size once expanded. | supported | FND-DATA-009, FND-DATA-010 |
| `0x07` | 4 | `UINT32LE` | `compressed_size` | Length of the file's block. | supported | FND-DATA-009, FND-DATA-010 |
| `0x0B` | 4 | `UINT32LE` | `offset` | Offset of the file's block; each follows the one before. | supported | FND-DATA-009 |
| `0x0F` | 2 | `UINT16LE` | `date` | MS-DOS date of the file. | supported | FND-DATA-009 |
| `0x11` | 2 | `UINT16LE` | `time` | MS-DOS time of the file. | supported | FND-DATA-009 |
| `0x13` | 4 | `UINT32LE` | `attributes` | MS-DOS file attributes, `0x20` (archive) in every entry. | supported | FND-DATA-009 |
| `0x17` | 2 | `UINT16LE` | `entry_size` | Length of this entry; the next starts that many bytes on. | supported | FND-DATA-009 |
| `0x19` | 4 | `BYTE[4]` | `unk_19` | Zero in every entry. | supported | FND-DATA-010 |
| `0x1D` | 1 | `BYTE` | `name_length` | Length of `name`. | supported | FND-DATA-009 |
| `0x1E` | `name_length` | `CHAR[]` | `name` | The file's name. | supported | FND-DATA-009 |
| `0x1E + name_length` | 13 | `BYTE[13]` | `unk_tail` | Zero in every entry except that of `Chaos Overlords.exe`, whose fourth byte is `0x01`. | supported | FND-DATA-010 |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

`DATA/DATA.Z` of BLD-GOG-EN-1.1 was read with a script (FND-DATA-005,
FND-DATA-009). The header, the five directory entries and the 459 file
entries account for every byte of the file, and the blocks are contiguous
from `0xFF` to the directory entries. Every block was expanded with an
implode decoder (FND-DATA-010): all 459 reach the expanded size, 449 equal
the installed file of the same path, 9 differ from it (the executable and
eight images) and `README.DOC` is not installed. No code path in the
executable can open the file (FND-DATA-008), so the game never reads it.

## Open questions

- What the constant header bytes `0x04` to `0x0B` and `0x1A` to `0x28`, and
  the byte set only in the executable's file entry, stand for. The table
  gives their values; a second InstallShield 3 archive or the setup program
  would be needed to name them.
