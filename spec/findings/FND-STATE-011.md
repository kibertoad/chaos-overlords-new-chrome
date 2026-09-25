---
id: FND-STATE-011
title: The most used .data addresses the data map left unnamed are fields of known records, the modem and socket handles, and a per-connection flag array
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004854F0..0x00485D0F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048F824
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004905D4..0x004905E3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00490720..0x0049085F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00493220..0x0049324B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00493290..0x004932CF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00493598..0x004935CB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00493830..0x00493833
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00493FA4..0x00493FE6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494D30..0x00494D37
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498012..0x00498013
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004988B0..0x004988BB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB698..0x004AB6A3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004B1720
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004B1944
tool: Ghidra 12.1.3
environment: null
---

## Observation

This extends the data map of FND-STATE-007 and FND-STATE-008 with the
addresses `node tools/spec-coverage.mjs --inventory` listed on 2026-09-25 as
the most used ones no entry cited. Most lie inside regions those findings
already bound; the rows name the field. An indexed access such as
`[EAX + 0x004905D4]` is counted by Ghidra against its base address, so a base
address below stands for the same field of every record. Ranges of the
functions named are in FND-EXE-004.

Fields of records already mapped:

| Address | Record and field | Writers | Readers | Evidence |
|---|---|---|---|---|
| `0x004905D4` | movie slot 0 (`0x004905C0`, 36 bytes) +0x14, the `SmackBuf` handle | `fn_0040DAE0` (`0x0040DB57`), `fn_0040DBC0` (`0x0040DCAD`) | `fn_0040DBC0`, `fn_0040DD7B`, `fn_0040DDFF` | FND-VIDEO-002 |
| `0x004905D8`, `0x004905DC` | the same slot +0x18 and +0x1C, the destination rectangle's top and left, bottom and right | `fn_0040DAE0` (`0x0040DB90`), `fn_0040DBC0` (`0x0040DC40`) | `fn_0040DDFF` (`0x0040DF39`) | FND-VIDEO-002 |
| `0x004905E0` | the same slot +0x20, the volume | `fn_0040DAE0` (`0x0040DBA8`), `fn_0040E049` (`0x0040E081`) | `fn_0040E02B` (`0x0040E037`) | FND-VIDEO-002 |
| `0x00490720..0x0049085F` | connection 0 (`0x004906D0`, `0x350` bytes) +0x50, ten 32-byte texts | cleared by `fn_004211E0` (`0x00421460`); the serial set-up `fn_00424FB4` copies the port chosen in combo box 1001 into the first (`0x004251A4`) | `fn_00424FB4`, which passes it to `CommConfigDialogA` (`0x004251C8`) | FND-STATE-008 |
| `0x00493FA4`, `0x00493FA8`, `0x00493FAC` | file slot 0 (`0x00493F90`, `0x15C` bytes) +0x14, +0x18 and +0x1C: the dialog's filter, custom filter and custom filter size, fields of the `OPENFILENAMEA` at +8; the last two are set to 0 | `fn_0042AFDD` (`0x0042B081` to `0x0042B0B3`), `fn_0042B27A` (`0x0042B393` to `0x0042B3C5`) | the file dialogs those two open | FND-PLATFORM-010 |
| `0x00493FB4`, `0x00493FB8` | file slot 0 +0x24 and +0x28: the file buffer pointer and its size, 260 (`0x104`) | `fn_0042AFDD`, `fn_0042B27A`, `fn_0042B60F` | `fn_0042AC7A`, `fn_0042B27A`, `fn_0042B60F` | FND-PLATFORM-010 |
| `0x00493FCC` to `0x00493FD4` | file slot 0 +0x3C, +0x40, +0x42 and +0x44: the dialog's flags, the two 16-bit name offsets and the default extension pointer | `fn_0042AFDD` (flags `0x1810`, extension `0x0048766C`, `0x0042B1A3` to `0x0042B20D`), `fn_0042B27A` (flags `0x816`, extension `0x004876AC`, `0x0042B4B5` to `0x0042B51F`); both set the offsets to 0 | the file dialogs those two open | FND-PLATFORM-010, FND-SAVE-002 |
| `0x00493FE6` | file slot 0 +0x56, set while the handle is open | `fn_0042AC7A` (`0x0042AD55`, `0x0042ADD7`), `fn_0042ADE9` (`0x0042AE73`) | `fn_0042ABFB`, `fn_0042AE85`, `fn_0042AF31` | FND-PLATFORM-010 |
| `0x00493830` | surface slot 10 (`0x00493658`, 44 bytes) +32, the bitmap of the slot every image load uses | `fn_00425FB0`, through the slot index (`0x004260A5`); 0 by `fn_00425850` (`0x0042596B`) | `fn_004273D5` (`0x0042765A`, `0x00427685`) and the uncalled `fn_00426F77` (`0x004272D9`, `0x00427304`), as the bitmap argument of `SetDIBits` | FND-GFX-004, FND-DATA-008 |
| `0x00494D30`, `0x00494D34` | sound slot 0 (`0x00494C28`, `0x114` bytes) +0x108 and +0x10C, the memory handle and the locked data | the loader `fn_0045867C` (`0x004587A4`, `0x004587EE`); 0 by the setup `fn_00458290` (`0x004583A2`, `0x0045838A`) and the unloader `fn_00458895` (`0x0045898B`, `0x004589A3`) | `fn_0045867C`, `fn_00458895`, and the player `fn_0045851A` (`0x0045853E`) | FND-AUDIO-006 |
| `0x00498012` | channel record 0 (`0x00498000`, 20 bytes) +0x12, the 16-bit sound slot playing on it | set to -1 by `fn_00458290` (`0x00458415`); `fn_0045851A` (`0x0045866A`) | `fn_00458895` (`0x004588F3`, `0x0045890E`) | FND-AUDIO-006 |
| `0x0048F824` | player-pair record (`0x0048F810`, 24 bytes, observer * 0x90 + other * 0x18) +0x14, the out-fight flag | `fn_0040A1A7`: cleared at `0x0040A28B`, set at `0x0040A859` | `fn_0041FEF0` (`0x0042085D`) | FND-STATE-007 |
| `0x004AB698` to `0x004AB6A2` | `site_definitions` entry 0 (`0x004AB668`, 62 bytes) +0x30 to +0x3A: the 16-bit modifiers to Research, Strength, Blade, Range, Fighting and Martial Arts (FMT-DATA-001) | none; read whole from the file | the sector bonus rebuild `fn_004782C5` (`0x004784DF` and on), which adds them to the gangs in the site's sector, and the computer players' selector function `fn_00402D70` (`0x0040323B` and on) | FND-STATE-007, FND-GANG-001 |

Modem state inside the modem region `0x004854D0..0x0048626B` of FND-STATE-008:

| Address | Element | Writers | Readers | What it is |
|---|---|---|---|---|
| `0x004854F0` | INT32 | set to 1 by the line openers `fn_0041C36E` (`0x0041C3E0`) and `fn_0041C735` (`0x0041C7A7`); 0 by `fn_0041C014` and the line closer `fn_0041C0E0` (`0x0041C305`) | those, and the wait loops `fn_0041CFCA`, `fn_0041D0A5` | the line is being opened or is open |
| `0x004854F4` | INT32 | `lineMakeCall` in `fn_0041CA2F`, through the address pushed at `0x0041CC34`; the Telephony callback `fn_0041D51B` (`0x0041D5CF`, `0x0041D605`); 0 by `fn_0041BE20`, `fn_0041C014`, `fn_0041C0E0`, `fn_0041D2FB` | `lineDial` in `fn_0041CA2F` (`0x0041CC5B`), `fn_0041DD24` (`lineGetCallStatus`), `lineDeallocateCall` in `fn_0041C0E0` (`0x0041C241`), `fn_0041D51B`, `fn_0041E303` | the call handle |
| `0x004854F8` | INT32 | `lineOpen`, through the address pushed at `0x0041C514`; 0 by `fn_0041BE20`, `fn_0041C014`, `fn_0041C0E0`, `fn_0041D2FB` | `lineClose` in `fn_0041C0E0` (`0x0041C289`), `fn_0041C36E`, `fn_0041C735`, `fn_0041CA2F` | the line handle |
| `0x004854FC` | INT32 | `fn_0041E8DF`: the chosen device at `0x0041EA5F`, -1 at `0x0041EB49` | `fn_0041C36E`, `fn_0041C735` (passed to `fn_0041DB3B` and `lineSetDevConfig`), `fn_0041CA2F`, `fn_0041F1CF`, `fn_0041F808` | the chosen line device, -1 for none |
| `0x00485500` | INT32 | `fn_0041C36E` (`0x0041C414`), `fn_0041C735` (`0x0041C7BF`), from `fn_0041DB3B` (`lineNegotiateAPIVersion`) | the same and `fn_0041CA2F`; 0 leads to a message box | the negotiated Telephony version |
| `0x00485504` | INT32 | `fn_0041D51B` from its fourth argument (`0x0041D52A`) | the wait loop `fn_0041D0A5` (`0x0041D0D5`) | the request the wait loop is waiting for |
| `0x00485508..0x00485907`, `0x00485908..0x00485D07` | 1,024 bytes each | filled by the functions they are passed to | passed by `fn_0041C36E` (`0x0041C69A`, `0x0041C69F`) and `fn_0041F808` (`0x0041FC3E`, `0x0041FC59`) to `fn_0041CA2F`; the first also by `fn_0041D51B` (`0x0041D926`, `0x0041D972`) | two text buffers of the dialling code |
| `0x00485D08` | INT32 | 1 by `fn_0041D51B` (`0x0041D862`), which does its set-up only while it is 0; 0 by `fn_0041C014`, `fn_0041C0E0` | `fn_0041D51B` | a once-per-call flag of the Telephony callback |
| `0x00485D0C` | pointer | `fn_0041F567` (`0x0041F63F`), from the allocator `fn_0041DA86`, filled by `lineGetDevConfig`; 0 by `fn_0041C0E0` after `LocalFree` (`0x0041C2D7`) | `fn_0041C36E`, `fn_0041C735` (passed to `lineSetDevConfig`), `fn_0041F567` | the device configuration block |

Socket and serial state:

| Address | Element | Writers | Readers | What it is |
|---|---|---|---|---|
| `0x00493220` | UINT8 | 1 by the listener `fn_004225FC` (`0x004226BA`); 0 by `fn_004211E0` and `fn_0042329C` (`0x004232C8`) | the shutdown cleanup `fn_004214BA` (`0x004214F6`), `fn_00456F80`, `fn_004677F0` | a listening socket is open |
| `0x00493238` | SOCKET | `fn_004225FC` from `socket` (`0x0042264C`); -1 by `fn_004211E0` (`0x00421240`) and by `fn_0042329C` after closing it (`0x004232B0`) | `accept` in `fn_00421D1A` (`0x00421DE8`); the window procedure `fn_0045C33B` (`0x0045C39E`), which treats a socket message for this socket as connection 13 | the listening socket |
| `0x0049323C` | address block | `fn_004225FC` | `bind` in `fn_004225FC` (`0x004226A3`) | the listening address |
| `0x00493290`, `0x004932B0` | 32-byte texts | `fn_00424AE5` (`0x00424C7A`, `0x00424C9B`, `0x00424CC3`) | the dialog procedure `fn_00465EC6` (`0x004660EA`) | entries 1 and 2 of this host's address texts at `0x00493270` (FND-UI-022), listed in dialog 20002 |
| `0x00493598..0x004935CB` | 52-byte serial configuration block | size 52 (`0x34`) by `fn_00424FB4` (`0x004251AA`); filled by `CommConfigDialogA` | `fn_00424FB4`; `SetCommConfig` in `fn_00424E55` (`0x00424F2C`) | the serial port configuration |

Per-connection flags between the regions of FND-STATE-008:

| Address | Element | Writers | Readers | What it is |
|---|---|---|---|---|
| `0x004988B0..0x004988BB` | UINT8[12], one per connection | 1 by the host lobby `fn_004677F0` (`0x004685B8`) and the load-session screen `fn_00456F80` (`0x00457951`) just before they close a connection with `fn_00421A2D`, 0 by them for the others (`0x004685D3`, `0x0045796C`); 1 by the packet dispatcher `fn_0046BA84` (`0x0046CBB2`) and by the session setup `fn_0046D22F` (`0x0046D6EF`) | `fn_0046D22F` (`0x0046D6BD`) | the connection has been given up: the host's wait loop in `fn_0046D22F` skips it, and marks a connection that `fn_00424208` reports not ready (FND-NET-005) before it handles the players seated on it |

Resource addresses. The two `.rsrc` addresses the inventory lists,
`0x004B1720` and `0x004B1944`, are the templates of dialogs 129 and 131.
Ghidra links them to the call sites that push those IDs before
`fn_00465CEC` (FND-UI-022): `0x00439C7A`, `0x00439D7B`, `0x00470458`,
`0x0047054E`, `0x0047093D`, `0x004721AC` and `0x0047229C` for 129, and
`0x0040D37B` and `0x00462C5A` for 131. The code reads neither address.

## Interpretation

Of these addresses, only two groups reach the rules: the six skill modifiers
of each site definition, which the sector bonus rebuild adds to the gangs in
a site's sector, and the player-pair flag the computer players compute and
read. The rest belongs to the movie, file, sound and drawing layers, whose
records FND-STATE-008 already names, or to the modem, socket and serial code
that DEV-NET-001 leaves out.

## Alternatives

- The roles of the two 1,024-byte modem buffers and of `0x00485D08` are read
  from the functions they are passed to and not traced further, since the
  modem code is out of scope.
- FND-STATE-008 places 32-byte name records at `0x00493220`. The reads and
  writes listed here use `0x00493220` as a flag with the socket at +0x18 and the
  address at +0x1C; the 32-byte texts start at `0x00493270`.

## How to reproduce

List the references to each address with the references view, and for an
indexed access read the scale and the base register to find the record size.
For the resource addresses, read the resource directory for dialogs 129 and
131.
