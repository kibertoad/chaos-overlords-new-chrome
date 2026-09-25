---
id: FND-ASSET-001
title: The executable names its data files by fixed relative paths and five-digit templates
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487BE8..0x00487C0B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487500..0x00487533
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048770C..0x0048772A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004877A0..0x004877C3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048789C..0x004878C3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487900..0x00487913
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487AF4..0x00487B06
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B44..0x00487B57
tool: Ghidra 12.1.3
environment: null
---

## Observation

These NUL-terminated strings are in `.data`:

| Address | String |
|---|---|
| `0x00487C00` | `data\Sites` |
| `0x00487BE8` | `data\Gangs` |
| `0x00487BF4` | `data\Items` |
| `0x00487500`, `0x00487900` | `data\PX08\PX00000` |
| `0x0048789C` | ` data\PX08\px00128` (leading space) |
| `0x004878B0` | ` data\PX16\px00128` (leading space) |
| `0x00487514`, `0x00487524` | `data\CLT00000` |
| `0x004877A0`, `0x004877B4` | `data\snd00000` |
| `0x0048771C` | `Data\mvIntro` |
| `0x0048770C` | `Data\mvLogos` |
| `0x00487AF4` | `.\Help\Chaos.hlp` |
| `0x00487B44` | ` A:\CHAOS\CDTrack` (leading space) |

No string names `DATA.Z`, and none names the `MUSIC` directory or an Ogg file.

## Interpretation

The game opens its tables, movies and help file by fixed paths relative to the
current directory, and its images, sounds and palette by templates whose five
zeros are replaced with a resource number (FND-PLATFORM-002 shows this for
images). The paths differ in case from the installed names (`DATA/SITES`,
`DATA/ITEMS`, `DATA/MVINTRO`), which is harmless on Windows's case-insensitive
file systems. `A:\CHAOS\CDTrack` looks like a leftover or a template for CD
audio, which the game otherwise plays through MCI (FND-AUDIO-001).

## Alternatives

The leading spaces may be placeholders that the code overwrites with a drive
letter or another character before use; that has not been checked. The role of
`A:\CHAOS\CDTrack` has not been traced to a caller.

## How to reproduce

Search `Chaos Overlords.exe` for the strings above; each address is a virtual
address in `.data` (file offset = address - `0x00482000` + `0x80E00`).
