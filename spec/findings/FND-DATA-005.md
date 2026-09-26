---
id: FND-DATA-005
title: DATA/DATA.Z starts with the bytes 13 5D 65 8C and is not named by the executable
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
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00400000..0x004C9000
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

- `DATA/DATA.Z` is 7,676,546 bytes. It begins `13 5D 65 8C 3A 01 02 00`.
- The byte values over the whole file have an entropy of 7.97 bits per byte.
- `Chaos Overlords.exe` contains no string `DATA.Z` or `data.z` in any case.

## Interpretation

The file is compressed data that the game does not open by name. Read as a
little-endian DWORD, its first four bytes are `0x8C655D13`, the signature of
the compressed archives that InstallShield 3 installers carry. It is most
likely a leftover of the original CD installer.

## Alternatives

The executable could build the name from parts; no string with `.Z` supports
that. The archive signature has not been confirmed by extracting the file with
an InstallShield tool.

## How to reproduce

Read the first bytes of `DATA/DATA.Z`, and search `Chaos Overlords.exe` for
`DATA.Z` ignoring case.
