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
definition: null
evidence: [FND-DATA-005]
conflicting: []
split_with: []
related: []
---

## Layout

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 4 | `UINT32LE` | `signature` | `0x8C655D13` (bytes `13 5D 65 8C`), the signature of an InstallShield 3 compressed archive. | unknown | FND-DATA-005 |
| `0x04` | 7676542 | `BYTE[7676542]` | `unk_04` | Compressed data, not decoded. | unknown | FND-DATA-005 |
| `0x752282` | | | | Total size 7676546 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

`DATA/DATA.Z` of BLD-GOG-EN-1.1 was read with a script (FND-DATA-005): the
first eight bytes and the byte entropy of the whole file. No definition exists
yet.

## Open questions

- The executable does not name this file (FND-DATA-005), so it is probably a
  leftover of the original installer and not game data. Extracting it with an
  InstallShield 3 tool would confirm the format and list its contents.
- The archive header after the signature has not been described.
