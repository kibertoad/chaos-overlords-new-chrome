---
id: FMT-DATA-005
title: Compressed archive DATA/DATA.Z
status: unknown
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/DATA.Z"]
byte_order: little
size: 7676546
text: false
definition: fmt_data_005.ksy
evidence: [FND-DATA-005, FND-DATA-008, FND-DATA-009]
conflicting: []
split_with: []
related: []
---

## Layout

The header:

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `UINT32LE` | `signature` | `0x8C655D13` (bytes `13 5D 65 8C`), the signature of an InstallShield 3 compressed archive. | supported | FND-DATA-005, FND-DATA-009 |
| `0x04` | 8 | `BYTE[8]` | `unk_04` | Not interpreted. | unknown | FND-DATA-009 |
| `0x0C` | 2 | `UINT16LE` | `file_count` | Number of file entries, 459. | supported | FND-DATA-009 |
| `0x0E` | 2 | `UINT16LE` | `date` | MS-DOS date of the archive, 26 April 1996. | supported | FND-DATA-009 |
| `0x10` | 2 | `UINT16LE` | `time` | MS-DOS time of the archive, 15:54:50. | supported | FND-DATA-009 |
| `0x12` | 4 | `UINT32LE` | `archive_size` | Length of the whole file. | supported | FND-DATA-009 |
| `0x16` | 19 | `BYTE[19]` | `unk_16` | Not interpreted. | unknown | FND-DATA-009 |
| `0x29` | 4 | `UINT32LE` | `dir_table_offset` | Offset of the directory entries, which follow the last data block. | supported | FND-DATA-009 |
| `0x2D` | 4 | `BYTE[4]` | `unk_2d` | Not interpreted. | unknown | FND-DATA-009 |
| `0x31` | 2 | `UINT16LE` | `dir_count` | Number of directory entries, 5. | supported | FND-DATA-009 |
| `0x33` | 4 | `UINT32LE` | `file_table_offset` | Offset of the file entries, right after the directory entries. | supported | FND-DATA-009 |
| `0x37` | 2 | `UINT16LE` | `file_table_size` | Length of the file entries, which end the file. | supported | FND-DATA-009 |
| `0x39` | 198 | `BYTE[198]` | `unk_39` | Not interpreted. | unknown | FND-DATA-009 |
| `0xFF` | | `BYTE[]` | `blocks` | The compressed files back to back, in file-table order, up to `dir_table_offset`. | supported | FND-DATA-009 |

A directory entry, `dir_count` of them from `dir_table_offset`:

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 2 | `UINT16LE` | `file_count` | Files whose `dir_index` is this entry's index. | supported | FND-DATA-009 |
| `0x02` | 2 | `UINT16LE` | `entry_size` | Length of this entry; the next starts that many bytes on. | supported | FND-DATA-009 |
| `0x04` | 2 | `UINT16LE` | `name_length` | Length of `name`. | supported | FND-DATA-009 |
| `0x06` | `name_length` | `CHAR[]` | `name` | The directory's path, empty for the root. | supported | FND-DATA-009 |

A file entry, `file_count` of them from `file_table_offset`:

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `BYTE` | `unk_00` | Not interpreted. | unknown | FND-DATA-009 |
| `0x01` | 2 | `UINT16LE` | `dir_index` | Index of the file's directory entry. | supported | FND-DATA-009 |
| `0x03` | 4 | `UINT32LE` | `expanded_size` | The file's size once expanded. | supported | FND-DATA-009 |
| `0x07` | 4 | `UINT32LE` | `compressed_size` | Length of the file's block. | supported | FND-DATA-009 |
| `0x0B` | 4 | `UINT32LE` | `offset` | Offset of the file's block; each follows the one before. | supported | FND-DATA-009 |
| `0x0F` | 2 | `UINT16LE` | `date` | MS-DOS date of the file. | supported | FND-DATA-009 |
| `0x11` | 2 | `UINT16LE` | `time` | MS-DOS time of the file. | supported | FND-DATA-009 |
| `0x13` | 4 | `UINT32LE` | `attributes` | MS-DOS file attributes, `0x20` (archive) in every entry. | supported | FND-DATA-009 |
| `0x17` | 2 | `UINT16LE` | `entry_size` | Length of this entry; the next starts that many bytes on. | supported | FND-DATA-009 |
| `0x19` | 4 | `BYTE[4]` | `unk_19` | Not interpreted. | unknown | FND-DATA-009 |
| `0x1D` | 1 | `BYTE` | `name_length` | Length of `name`. | supported | FND-DATA-009 |
| `0x1E` | `name_length` | `CHAR[]` | `name` | The file's name. | supported | FND-DATA-009 |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

`DATA/DATA.Z` of BLD-GOG-EN-1.1 was read with a script (FND-DATA-005,
FND-DATA-009). The header, the five directory entries and the 459 file
entries account for every byte of the file, and the blocks are contiguous
from `0xFF` to the directory entries. No code path in the executable can open
the file (FND-DATA-008), so the game never reads it. The blocks were not
decompressed.

## Open questions

- Whether the blocks expand to the installed files, which extracting them
  with an InstallShield 3 tool would show.
- The header and entry bytes marked unknown.
