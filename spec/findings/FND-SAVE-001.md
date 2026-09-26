---
id: FND-SAVE-001
title: A save is a marker, 44 global blocks in a fixed order, an optional network block and the marker again
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046381A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463CC5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004637B8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449C8E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487850..0x0048785C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498968..0x00498980
tool: Ghidra 12.1.3
environment: null
---

## Observation

Load function `0x0046381A` and save function `0x00463CC5` move the whole file.
The first four bytes are a marker, compared and written as one DWORD:

| Bytes on disk | DWORD | Payload | File size | Load result |
|---|---|---|---|---|
| `S40W` | `0x57303453` | 44 blocks | 45,305 (`0xB0F9`) | 1 |
| `N40W` | `0x5730344E` | 44 blocks, then one 24-byte block | 45,329 (`0xB111`) | 2 |
| `M10W` | `0x5730314D` | one 12-byte block | 16 (`0x10`) | 3 |

The 44 blocks total 45,297 (`0xB0F1`) bytes and are moved in this order, each
given as the global it is copied from or to, and its size in bytes:

| # | Global | Size | # | Global | Size |
|---|---|---|---|---|---|
| 1 | `0x00498DA8` | 15552 | 23 | `0x004ABCC0` | 256 |
| 2 | `0x004A08E8` | 2304 | 24 | `0x004AAE08` | 1920 |
| 3 | `0x004ABBF0` | 24 | 25 | `0x004A8888` | 9600 |
| 4 | `0x004A5F00` | 6 | 26 | `0x004A11E8` | 4860 |
| 5 | `0x004AB638` | 24 | 27 | `0x00494830` | 2 |
| 6 | `0x0049CA68` | 4 | 28 | `0x004A2588` | 72 |
| 7 | `0x004ABBE8` | 4 | 29 | `0x004A27A8` | 24 |
| 8 | `0x004A5EF8` | 4 | 30 | `0x004A5ED8` | 24 |
| 9 | `0x004A25E8` | 24 | 31 | `0x004A25D0` | 24 |
| 10 | `0x004ABBC0` | 18 | 32 | `0x0049CA78` | 24 |
| 11 | `0x004A27C8` | 18 | 33 | `0x004AB620` | 24 |
| 12 | `0x004A2608` | 384 | 34 | `0x004A27E0` | 24 |
| 13 | `0x004ABBE0` | 6 | 35 | `0x004AB590` | 144 |
| 14 | `0x0048A250` | 7776 | 36 | `0x004AB650` | 24 |
| 15 | `0x00482108` | 6 | 37 | `0x004A2600` | 6 |
| 16 | `0x00482110` | 24 | 38 | `0x004A2570` | 24 |
| 17 | `0x0048DB48` | 1944 | 39 | `0x004ABC58` | 6 |
| 18 | `0x00482158` | 6 | 40 | preference 1 | 1 |
| 19 | `0x0048E2F8` | 24 | 41 | preference 2 | 1 |
| 20 | `0x00482128` | 24 | 42 | preference 3 | 1 |
| 21 | `0x00482140` | 24 | 43 | `0x004AB588` | 6 |
| 22 | `0x004A2790` | 24 | 44 | `0x004A5EF0` | 6 |

- The save function writes the three preference bytes from the globals
  `0x00487850`, `0x00487854` and `0x00487858`. The load function reads them
  into `0x0049833C`, `0x00498340` and `0x00498300` and copies them to the live
  globals only after the closing marker has been read and matched.
- An `N40W` file continues with the 24-byte block at `0x00498968`, which holds
  six DWORD participant mappings and is filled only while the network-mode flag
  `0x00487B58` is set.
- Both long forms end with a second copy of the opening marker. An `M10W` file
  has no closing marker.
- Neither function checks the byte count of any of its 46 or 47 reads or
  writes.
- A wrong opening or closing marker makes the load function beep through
  `0x00449C8E`. Its only caller, `0x004637B8`, shows the old-version message
  only when the load returns 0.

## Interpretation

A save file is a direct copy of the game's global state, in a fixed order with
no padding, framed by the marker. `S40W` is the ordinary full save and `N40W`
the same with the network participant table added. `M10W` is accepted but is
not a full save: its 12-byte block has no other reference in the code, so what
it is for is unknown. The closing marker is only a structural check; nothing
guards against a short read, so a truncated file leaves part of the state
overwritten and part as it was.

## Alternatives

None known for the envelope. What each block holds is read from the globals
it copies, which other findings identify one at a time.

## How to reproduce

Search the code for the immediate `0x57303453`: its bytes appear at
`0x0046385A`, inside the load function, and at `0x00463D56`, inside the save
function. The block list is the
sequence of read and write wrapper calls (FND-PLATFORM-003) in each function.
