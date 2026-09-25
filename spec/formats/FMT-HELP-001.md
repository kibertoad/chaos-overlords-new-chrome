---
id: FMT-HELP-001
title: WinHelp container HELP/Chaos.hlp
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["HELP/Chaos.hlp"]
byte_order: little
size: 60208
text: false
definition: fmt_help_001.ksy
evidence: [FND-HELP-003, FND-HELP-001, FND-HELP-002, FND-ASSET-001, FND-DATA-006]
conflicting: []
split_with: []
related: []
---

## Layout

The help file is a WinHelp 3.1 container: a 16-byte header, then internal
files, each with a 9-byte header, and a directory that names them. The
executable opens it by the path `.\Help\Chaos.hlp` (FND-ASSET-001) and leaves
reading it to the Windows help viewer. The field names follow the public
description of the WinHelp format. This entry describes the container, the
directory and the internal file headers; the contents of the internal files
are left as blocks of bytes, and FND-HELP-001 and FND-HELP-002 record what
was read from `|CONTEXT`, `|CTXOMAP`, `|FONT` and `|TOPIC`.

### File header

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `UINT32LE` | `magic` | `0x00035F3F`. | supported | FND-HELP-003 |
| `0x04` | 4 | `INT32LE` | `directory_offset` | Offset of the directory's internal file header, `0xE42`. | supported | FND-HELP-003 |
| `0x08` | 4 | `INT32LE` | `first_free_block` | -1: no free blocks. | supported | FND-HELP-003 |
| `0x0C` | 4 | `UINT32LE` | `file_size` | 60,208, the size of the file. | supported | FND-HELP-003 |
| `0x10` | | | | Total size 16 | | |

### Internal file header

Every internal file, the directory included, starts with this header at the
offset the directory (or `directory_offset`) gives.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `UINT32LE` | `reserved_size` | Bytes the internal file occupies, this header included. The next internal file starts right after them. | supported | FND-HELP-003 |
| `0x04` | 4 | `UINT32LE` | `used_size` | Bytes of content after this header, `reserved_size` minus 9 in every internal file. | supported | FND-HELP-003 |
| `0x08` | 1 | `UINT8` | `file_flags` | 4 for the directory, 0 for the other internal files. | supported | FND-HELP-003 |
| `0x09` | `used_size` | `BYTE[used_size]` | `content` | The internal file's content. For the directory it is the B+ tree header and the leaf page below. | supported | FND-HELP-003 |
| | | | | Total size 9 + used_size | | |

### Directory B+ tree header

Starts at `directory_offset + 9`.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 2 | `UINT16LE` | `btree_magic` | `0x293B`. | supported | FND-HELP-003 |
| `0x02` | 2 | `UINT16LE` | `btree_flags` | `0x0402`. | supported | FND-HELP-003 |
| `0x04` | 2 | `UINT16LE` | `page_size` | 1024. | supported | FND-HELP-003 |
| `0x06` | 16 | `char[16]` | `structure` | `z4` followed by NUL bytes: each entry is a NUL-terminated name and a 32-bit value. | supported | FND-HELP-003 |
| `0x16` | 2 | `UINT16LE` | `must_be_zero` | 0. | supported | FND-HELP-003 |
| `0x18` | 2 | `UINT16LE` | `page_splits` | 0. | supported | FND-HELP-003 |
| `0x1A` | 2 | `UINT16LE` | `root_page` | 0. | supported | FND-HELP-003 |
| `0x1C` | 2 | `INT16LE` | `must_be_minus_one` | -1. | supported | FND-HELP-003 |
| `0x1E` | 2 | `UINT16LE` | `total_pages` | 1. | supported | FND-HELP-003 |
| `0x20` | 2 | `UINT16LE` | `levels` | 1: the root page is the only leaf page. | supported | FND-HELP-003 |
| `0x22` | 4 | `UINT32LE` | `total_entries` | 11. | supported | FND-HELP-003 |
| `0x26` | | | | Total size 38 | | |

### Directory leaf page

Follows the B+ tree header and is `page_size` bytes long.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 2 | `UINT16LE` | `unused_bytes` | 879: bytes left free at the end of the page. | supported | FND-HELP-003 |
| `0x02` | 2 | `UINT16LE` | `entry_count` | 11. | supported | FND-HELP-003 |
| `0x04` | 2 | `INT16LE` | `previous_page` | -1. | supported | FND-HELP-003 |
| `0x06` | 2 | `INT16LE` | `next_page` | -1. | supported | FND-HELP-003 |
| `0x08` | `page_size - 8 - unused_bytes` | `BYTE[page_size - 8 - unused_bytes]` | `entries` | `entry_count` entries sorted by name in byte order, each a NUL-terminated internal file name and a `UINT32LE` offset of its internal file header. | supported | FND-HELP-003 |
| | `unused_bytes` | `BYTE[unused_bytes]` | `free_space` | Not read. | supported | FND-HELP-003 |
| | | | | Total size page_size | | |

## Enumerations and flags

None.

## Differences between builds

None known.

## Coverage

`HELP/Chaos.hlp` of BLD-GOG-EN-1.1 was read with a script (FND-HELP-003):
every value in the tables was read from the file, the eleven internal files
and the directory cover the file from `0x10` to its end without gaps, and each
internal file header's `reserved_size` ends it where the next begins. The
contents of `|PhrImage`, `|PhrIndex`, `|SYSTEM` beyond its first fields,
`|KWDATA`, `|KWMAP`, `|KWBTREE` and `|TTLBTREE` were not decoded. The Kaitai
definition compiles and parses the file (FND-DATA-006).

## Open questions

- The public description of the WinHelp format is not yet an entry of this
  spec, so the field names and the reading of `|SYSTEM` flags 4 as compressed
  topics rest on it without a citable source.
- The topic compression (phrase replacement and LZ77) is not described here.
  The game never decodes it; only the help viewer does.
