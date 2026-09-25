---
id: FMT-SAVE-002
title: Short M10W save file
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 16
text: false
definition: fmt_save_002.ksy
evidence: [FND-SAVE-001, FND-DATA-006, FND-SAVE-002, FND-PLATFORM-009]
conflicting: []
split_with: []
related: []
---

## Layout

The load function accepts a third marker, `M10W`, followed by one 12-byte
block and nothing else, and returns 3 for it (FND-SAVE-001). No closing marker
follows. Nothing in the executable writes this form: the save writes only
`S40W` or `N40W` (FND-SAVE-002). On the result 3 `WinMain` calls routines
that do nothing and ends the program (FND-PLATFORM-009). The game ships no save files, so `files` is empty.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `UINT32LE` | `magic` | The marker. | supported | FND-SAVE-001 |
| `0x04` | 12 | `BYTE[12]` | `payload` | A 12-byte block read into `0x00498330`, which no other instruction references, so its meaning is unknown. | supported | FND-SAVE-001 |
| `0x10` | | | | Total size 16 | | |

## Enumerations and flags

### `magic`

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| `0x5730314D` | `SAVE_MAGIC_M10W` | `M10W` on disk. | supported | FND-SAVE-001 |

## Differences between builds

None known.

## Coverage

No file of this form was examined; the layout was read from the load function
of BLD-GOG-EN-1.1 (FND-SAVE-001). The Kaitai definition compiles; no file of this form exists to run it against
(FND-DATA-006).

## Open questions

- What the 12 bytes were meant to hold. The form looks like an unfinished
  feature (FND-SAVE-002).
